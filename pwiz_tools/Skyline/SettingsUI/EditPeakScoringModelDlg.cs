/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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
using ZedGraph;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.SettingsUI
{
    public partial class EditPeakScoringModelDlg : FormEx
    {
        private const int HISTOGRAM_BAR_COUNT = 20;
        private readonly Color _targetColor = Color.DarkBlue;
        private readonly Color _decoyColor = Color.OrangeRed;
        private readonly Color _secondBestColor = Color.Gold;

        private const int MPROPHET_MODEL_INDEX = 0;
        private const int SKYLINE_LEGACY_MODEL_INDEX = 1;

        private readonly IEnumerable<IPeakScoringModel> _existing;
        private IPeakScoringModel _peakScoringModel;
        private IPeakScoringModel _originalPeakScoringModel;
        private const string UNNAMED = "<UNNAMED>"; // Not L10N
        private string _originalName;
        private TargetDecoyGenerator _targetDecoyGenerator;
        private readonly PeakCalculatorGridViewDriver _gridViewDriver;
        private int _selectedCalculator = -1;
        private int _selectedIndex = -1;
        private SortableBindingList<PeakCalculatorWeight> PeakCalculatorWeights { get { return _gridViewDriver.Items; } }
        
        /// <summary>
        /// Create a new scoring model, or edit an existing model.
        /// </summary>
        /// <param name="existing">Existing scoring models (to check for unique name)</param>
        public EditPeakScoringModelDlg(IEnumerable<IPeakScoringModel> existing)
        {
            InitializeComponent();

            Icon = Resources.Skyline;

            _existing = existing;

            _gridViewDriver = new PeakCalculatorGridViewDriver(gridPeakCalculators, bindingPeakCalculators,
                                                            new SortableBindingList<PeakCalculatorWeight>());

            InitGraphPane(zedGraphMProphet.GraphPane);
            InitGraphPane(zedGraphSelectedCalculator.GraphPane);

            lblColinearWarning.Visible = false;

            gridPeakCalculators.DataBindingComplete += OnDataBindingComplete;
            comboModel.SelectedIndexChanged += comboModel_SelectedIndexChanged;
        }

        /// <summary>
        /// Get/set the peak scoring model.
        /// </summary>
        public IPeakScoringModel PeakScoringModel
        {
            get { return _peakScoringModel; }
            set { SetScoringModel(value); }
        }

        private void SetScoringModel(IPeakScoringModel scoringModel)
        {
            if (_originalName == null)
                _originalName = (scoringModel != null) ? scoringModel.Name : UNNAMED;

            // Scoring model is null if we're creating a new model from scratch (default to mProphet).
            _peakScoringModel = scoringModel ?? new MProphetPeakScoringModel(ModelName);

            ModelName = _peakScoringModel.Name;

            _targetDecoyGenerator = new TargetDecoyGenerator(_peakScoringModel);

            var mProphetModel = _peakScoringModel as MProphetPeakScoringModel;
            if (mProphetModel != null)
            {
                comboModel.SelectedIndex = _selectedIndex = MPROPHET_MODEL_INDEX;
                lblColinearWarning.Visible = mProphetModel.ColinearWarning;
            }
            else
            {
                comboModel.SelectedIndex = _selectedIndex = SKYLINE_LEGACY_MODEL_INDEX;
            }

            InitializeCalculatorGrid();

            if (_originalPeakScoringModel == null)
                _originalPeakScoringModel = _peakScoringModel;

            textMean.Text = DoubleToString(_peakScoringModel.DecoyMean);
            textStdev.Text = DoubleToString(_peakScoringModel.DecoyStdev);

            decoyCheckBox.Checked = _peakScoringModel.UsesDecoys;
            secondBestCheckBox.Checked = _peakScoringModel.UsesSecondBest;
            UpdateCalculatorGraph(0);
            UpdateModelGraph();
        }

        private static string DoubleToString(double d)
        {
            return double.IsNaN(d) ? string.Empty : d.ToString("F3");   // Not L10N
        }

        /// <summary>
        /// Get the number format used for displaying weights in calculator grid.
        /// </summary>
        public string PeakCalculatorWeightFormat { get { return gridPeakCalculators.Columns[1].DefaultCellStyle.Format; } }

        /// <summary>
        /// Train mProphet scoring model.
        /// </summary>
        public void TrainModel()
        {
            // Get scores for target and decoy groups.
            List<IList<double[]>> targetTransitionGroups;
            List<IList<double[]>> decoyTransitionGroups;
            _targetDecoyGenerator.GetTransitionGroups(out targetTransitionGroups, out decoyTransitionGroups);
            // Use decoys for training only if decoy box is checked
            if (!decoyCheckBox.Checked)
                decoyTransitionGroups = new List<IList<double[]>>();

            // Create new scoring model using the active calculators.
            switch (comboModel.SelectedIndex)
            {
                case MPROPHET_MODEL_INDEX:
                    _peakScoringModel = new MProphetPeakScoringModel(_peakScoringModel.Name, _targetDecoyGenerator.Weights, _peakScoringModel.PeakFeatureCalculators);  
                    break;
            }

            // Train the model.
            _peakScoringModel = _peakScoringModel.Train(targetTransitionGroups, decoyTransitionGroups, _targetDecoyGenerator.Weights, secondBestCheckBox.Checked);

            // Copy weights to grid.
            for (int i = 0; i < _gridViewDriver.Items.Count; i++)
            {
                double weight = _peakScoringModel.Weights[i];
                _gridViewDriver.Items[i].Weight = double.IsNaN(weight) ? null : (double?) weight;
                _targetDecoyGenerator.Weights[i] = weight;
            }
            gridPeakCalculators.Invalidate();

            // Copy mean and stdev to text boxes.
            textMean.Text = DoubleToString(_peakScoringModel.DecoyMean);
            textStdev.Text = DoubleToString(_peakScoringModel.DecoyStdev);
            lblColinearWarning.Visible = _peakScoringModel is MProphetPeakScoringModel && ((MProphetPeakScoringModel)_peakScoringModel).ColinearWarning;

            UpdateCalculatorGraph();
            UpdateModelGraph();
        }

        /// <summary>
        /// Close and accept the dialog.
        /// </summary>
        public void OkDialog()
        {
            // TODO: Remove this
            var e = new CancelEventArgs();
            var helper = new MessageBoxHelper(this);

            string name;
            if (!helper.ValidateNameTextBox(e, textName, out name))
                return;

            if (!Equals(name, _originalName) &&
                _existing != null &&
                _existing.Contains(r => Equals(name, r.Name)))
            {
                helper.ShowTextBoxError(textName, Resources.EditPeakScoringModelDlg_OkDialog_The_peak_scoring_model__0__already_exists, name);
                e.Cancel = true;
                return;
            }

            double decoyMean;
            if (!helper.ValidateDecimalTextBox(e, textMean, out decoyMean))
                return;

            double decoyStdev;
            if (!helper.ValidateDecimalTextBox(e, textStdev, out decoyStdev))
                return;

            DialogResult = DialogResult.OK;

            var mProphetModel = _peakScoringModel as MProphetPeakScoringModel;
            if (mProphetModel != null)
            {
                _peakScoringModel = new MProphetPeakScoringModel(
                    name,
                    mProphetModel.Weights,
                    mProphetModel.PeakFeatureCalculators,
                    decoyMean,
                    decoyStdev,
                    decoyCheckBox.Checked,
                    secondBestCheckBox.Checked,
                    mProphetModel.ColinearWarning);
                return;
            }

            var legacyModel = _peakScoringModel as LegacyScoringModel;
            if (legacyModel != null)
            {
                _peakScoringModel = new LegacyScoringModel(name, decoyMean, decoyStdev);
            }
        }

        public static bool IsUnknown(double d)
        {
            return (double.IsNaN(d) || double.IsInfinity(d));
        }

        /// <summary>
        /// Create weights array.  The given index will have a value of 1, all the others
        /// will have a value of NaN.
        /// </summary>
        private double[] CreateWeightsSelect(int index)
        {
            var weights = new double[_peakScoringModel.PeakFeatureCalculators.Count];
            for (int i = 0; i < weights.Length; i++)
                weights[i] = double.NaN;
            weights[index] = 1;
            return weights;
        }

        private static void ProcessScores(double score, ref double min, ref double max, ref int countUnknownScores)
        {
            if (IsUnknown(score))
                countUnknownScores++;
            else
            {
                min = Math.Min(min, score);
                max = Math.Max(max, score);
            }
        }

        #region Graphs

        /// <summary>
        /// Initialize the given Zedgraph pane.
        /// </summary>
        private static void InitGraphPane(GraphPane graphPane)
        {
            graphPane.Title.IsVisible = false;
            graphPane.XAxis.Title.Text = Resources.EditPeakScoringModelDlg_InitGraphPane_Score;
            graphPane.YAxis.Title.Text = Resources.EditPeakScoringModelDlg_InitGraphPane_Peak_count;
            graphPane.XAxis.MinSpace = 50;
            graphPane.XAxis.MajorTic.IsAllTics = false;
            graphPane.YAxis.MajorTic.IsAllTics = false;
            graphPane.XAxis.MinorTic.IsAllTics = false;
            graphPane.YAxis.MinorTic.IsAllTics = false;
            graphPane.Chart.Border.IsVisible = false;
            graphPane.Border.Color = Color.LightGray;
            graphPane.XAxis.Title.FontSpec.Size = 14;
            graphPane.YAxis.Title.FontSpec.Size = 14;
            graphPane.IsFontsScaled = false;
        }

        /// <summary>
        /// Redraw the main model graph.
        /// </summary>
        private void UpdateModelGraph()
        {
            var graphPane = zedGraphMProphet.GraphPane;
            graphPane.CurveList.Clear();
            graphPane.GraphObjList.Clear();

            // Nothing to draw if we don't have a trained model yet.
            if (PeakScoringModel.Weights == null)
            {
                zedGraphMProphet.Refresh();
                return;
            }

            // Get binned scores for targets and decoys.
            PointPairList targetPoints;
            PointPairList decoyPoints;
            PointPairList secondBestPoints;
            double min;
            double max;
            bool hasUnknownScores;
            GetPoints(
                -1,
                out targetPoints,
                out decoyPoints,
                out secondBestPoints,
                out min,
                out max,
                out hasUnknownScores);

            // Add bar graphs.
            if (decoyCheckBox.Checked)
                graphPane.AddBar(Resources.EditPeakScoringModelDlg_UpdateModelGraph_Decoys, decoyPoints, _decoyColor);
            if (secondBestCheckBox.Checked)
                graphPane.AddBar(Resources.EditPeakScoringModelDlg_UpdateCalculatorGraph_Second_Best_Peaks, secondBestPoints, _secondBestColor);
            graphPane.AddBar(Resources.EditPeakScoringModelDlg_UpdateModelGraph_Targets, targetPoints, _targetColor);

            // Graph normal curve if we have values for the decoy mean and stdev.
            double decoyMean;
            double decoyStdev;
            if (double.TryParse(textMean.Text, out decoyMean) &&
                double.TryParse(textStdev.Text, out decoyStdev))
            {
                // Calculate scale value for normal curve by calculating area of decoy histogram.
                double yScaleDecoy = 0;
                double yScaleSecondBest = 0;
                double yScale = 0;
                double previousY = 0;
                foreach (var decoyPoint in decoyPoints)
                {
                    // Add trapezoidal area.
                    yScaleDecoy += Math.Max(previousY, decoyPoint.Y) - Math.Abs(previousY - decoyPoint.Y)/2;
                    previousY = decoyPoint.Y;
                }
                previousY = 0;
                foreach (var secondBestPoint in secondBestPoints)
                {
                    // Add trapezoidal area.
                    yScaleSecondBest += Math.Max(previousY, secondBestPoint.Y) - Math.Abs(previousY - secondBestPoint.Y) / 2;
                    previousY = secondBestPoint.Y;
                }
                if (_peakScoringModel.UsesDecoys && _peakScoringModel.UsesSecondBest)
                    yScale = (yScaleDecoy + yScaleSecondBest) / 2.0;
                else if (_peakScoringModel.UsesSecondBest)
                    yScale = yScaleSecondBest;
                else if (_peakScoringModel.UsesDecoys)
                    yScale = yScaleDecoy;
                double binWidth = (max - min)/HISTOGRAM_BAR_COUNT;
                yScale *= binWidth;

                // Expand graph range to accomodate the norm curve.
                min = Math.Min(min, decoyMean - 4*decoyStdev);
                max = Math.Max(max, decoyMean + 4*decoyStdev);

                // Calculate points on the normal curve.
                PointPairList normalCurve = new PointPairList();
                double xStep = binWidth/8;
                for (double x = min; x < max; x += xStep)
                {
                    double norm = Statistics.DNorm(x, decoyMean, decoyStdev);
                    double y = norm*yScale;
                    normalCurve.Add(x, y);
                }

                // Add normal curve to graph behind the histograms.
                Color color;
                string normLabel;
                const int alpha = 50;
                if (_peakScoringModel.UsesSecondBest)
                {
                    var mixColor = Color.FromArgb(
                        (_decoyColor.R + _secondBestColor.R)/2,
                        (_decoyColor.G + _secondBestColor.G)/2,
                        (_decoyColor.B + _secondBestColor.B)/2);
                    color = Color.FromArgb(alpha, _peakScoringModel.UsesDecoys ? mixColor : _secondBestColor);
                    normLabel = _peakScoringModel.UsesDecoys
                                    ? Resources.EditPeakScoringModelDlg_UpdateModelGraph_Combined_Norm
                                    : Resources.EditPeakScoringModelDlg_UpdateModelGraph_Second_Best_Peaks_Norm;
                }
                else
                {
                    color = Color.FromArgb(alpha, _decoyColor);
                    normLabel = Resources.EditPeakScoringModelDlg_UpdateModelGraph_Decoy_norm;
                }
                var curve = new LineItem(normLabel, normalCurve, Color.DarkGray, SymbolType.None)
                    {
                        Line =
                            {
                                Width = 2, 
                                Fill = new Fill(color),
                                IsAntiAlias = true, 
                                SmoothTension = 1
                            }
                    };
                graphPane.CurveList.Add(curve);
            }

            // Set graph axis range.
            if (min == max)
            {
                min -= 1;
                max += 1;
            }
            graphPane.XAxis.Scale.Min = min - (max - min) / HISTOGRAM_BAR_COUNT;
            graphPane.XAxis.Scale.Max = max;
            graphPane.AxisChange();
            zedGraphMProphet.Refresh();
        }

        /// <summary>
        /// Redraw the graph for the given calculator.
        /// </summary>
        /// <param name="calculatorIndex">Which calculator (-1 to redraw the current calculator)</param>
        private void UpdateCalculatorGraph(int calculatorIndex = -1)
        {
            // Don't redraw the same graph multiple times if nothing has changed.
            if (calculatorIndex >= 0)
            {
                if (_selectedCalculator == calculatorIndex)
                    return;
                _selectedCalculator = calculatorIndex;
            }

            var graphPane = zedGraphSelectedCalculator.GraphPane;
            graphPane.CurveList.Clear();
            graphPane.GraphObjList.Clear();

            PointPairList targetPoints;
            PointPairList decoyPoints;
            PointPairList secondBestPoints;
            double min;
            double max;
            bool hasUnknownScores;
            GetPoints(
                _selectedCalculator,
                out targetPoints,
                out decoyPoints,
                out secondBestPoints,
                out min, 
                out max,
                out hasUnknownScores);
            if (decoyCheckBox.Checked)
                graphPane.AddBar(Resources.EditPeakScoringModelDlg_UpdateModelGraph_Decoys, decoyPoints, _decoyColor);
            if (secondBestCheckBox.Checked)
                graphPane.AddBar(Resources.EditPeakScoringModelDlg_UpdateCalculatorGraph_Second_Best_Peaks, secondBestPoints, _secondBestColor);
            graphPane.AddBar(Resources.EditPeakScoringModelDlg_UpdateModelGraph_Targets, targetPoints, _targetColor);

            if (min == max)
            {
                min -= 1;
                max += 1;
            }

            graphPane.XAxis.Scale.Min = min - (max - min) / HISTOGRAM_BAR_COUNT;
            graphPane.XAxis.Scale.Max = max;

            // If the calculator produces unknown scores, split the graph and show a histogram
            // of the unknown scores on the right side of the graph.
            if (hasUnknownScores)
            {
                // Create "unknown" label.
                var unknownLabel = new TextObj(Resources.EditPeakScoringModelDlg_UpdateCalculatorGraph_unknown, 1.0, 1.02, CoordType.ChartFraction, AlignH.Right, AlignV.Top)
                    {
                        FontSpec = new FontSpec(graphPane.XAxis.Scale.FontSpec) { IsItalic = true }
                    };
                unknownLabel.FontSpec.Size -= 2;
                graphPane.GraphObjList.Add(unknownLabel);

                // Try to erase the axis labels below the histogram of unknown values.
                var hideAxisValues = new BoxObj(
                    0.87,
                    1.01,
                    0.13 + 0.03,
                    0.10,
                    Color.White, Color.White)
                    {
                        Location = { CoordinateFrame = CoordType.ChartFraction },
                        ZOrder = ZOrder.A_InFront
                    };
                graphPane.GraphObjList.Add(hideAxisValues);

                // Erase a portion of the axis to indicate discontinuous data.
                var xAxisGap = new BoxObj(
                    0.8,
                    0.99,
                    0.07,
                    0.02,
                    Color.White, Color.White)
                    {
                        Location = { CoordinateFrame = CoordType.ChartFraction },
                        ZOrder = ZOrder.A_InFront
                    };
                graphPane.GraphObjList.Add(xAxisGap);
            }

            // Calculate the Axis Scale Ranges
            graphPane.AxisChange();
            zedGraphSelectedCalculator.Refresh();
        }

        /// <summary>
        /// Get graph points for the selected calculator.
        /// </summary>
        /// <param name="selectedCalculator">Calculator index or -1 to get graph points for the composite scoring model.</param>
        /// <param name="targetBins">Binned score data points for target scores.</param>
        /// <param name="decoyBins">Binned score data points for decoys.</param>
        /// <param name="secondBestBins">Binned score data points for "false targets" -- second highest scoring peaks in target records.</param>
        /// <param name="min">Min score.</param>
        /// <param name="max">Max score.</param>
        /// <param name="hasUnknownScores">Returns true if the calculator returns unknown scores.</param>
        private void GetPoints(
            int selectedCalculator, 
            out PointPairList targetBins, 
            out PointPairList decoyBins,
            out PointPairList secondBestBins,
            out double min, 
            out double max,
            out bool hasUnknownScores)
        {
            double[] scoringWeights;
            double[] calculatorWeights;
            if (selectedCalculator == -1)
            {
                scoringWeights = calculatorWeights = _targetDecoyGenerator.Weights;
            }
            else
            {
                scoringWeights = CreateWeightsSelect(0);
                calculatorWeights = CreateWeightsSelect(selectedCalculator);
            }

            List<double> targetScores;
            List<double> decoyScores;
            List<double> secondBestScores;
            _targetDecoyGenerator.GetScores(scoringWeights, calculatorWeights, out targetScores, out decoyScores, out secondBestScores);
            
            // Find score range for targets and decoys
            min = float.MaxValue;
            max = float.MinValue;
            int countUnknownTargetScores = 0;
            int countUnknownDecoyScores = 0;
            int countUnknownSecondBestScores = 0;
            foreach (var score in targetScores)
                ProcessScores(score, ref min, ref max, ref countUnknownTargetScores);
            foreach (var score in decoyScores)
                ProcessScores(score, ref min, ref max, ref countUnknownDecoyScores);
            foreach (var score in secondBestScores)
                ProcessScores(score, ref min, ref max, ref countUnknownSecondBestScores);

            // Sort scores into bins.
            targetBins = new PointPairList();
            decoyBins = new PointPairList();
            secondBestBins = new PointPairList();
            double binWidth = (max - min)/HISTOGRAM_BAR_COUNT;

            if (countUnknownTargetScores == targetScores.Count &&
                countUnknownDecoyScores == decoyScores.Count &&
                countUnknownSecondBestScores == secondBestScores.Count)
            {
                min = -1;
                max = 1;
                binWidth = 2.0/HISTOGRAM_BAR_COUNT;
            }

            else
            {
                for (int i = 0; i < HISTOGRAM_BAR_COUNT; i++)
                {
                    targetBins.Add(new PointPair(min + i * binWidth + binWidth / 2, 0));
                    decoyBins.Add(new PointPair(min + i * binWidth + binWidth / 2, 0));
                    secondBestBins.Add(new PointPair(min + i * binWidth + binWidth / 2, 0));
                    if (binWidth.Equals(0))
                    {
                        binWidth = 1; // prevent divide by zero below for a single bin
                        break;
                    }
                }
                foreach (var score in targetScores)
                {
                    if (IsUnknown(score))
                        continue;
                    int bin = Math.Min(targetBins.Count - 1, (int) ((score - min)/binWidth));
                    targetBins[bin].Y++;
                }
                foreach (var score in decoyScores)
                {
                    if (IsUnknown(score))
                        continue;
                    int bin = Math.Min(decoyBins.Count - 1, (int) ((score - min)/binWidth));
                    decoyBins[bin].Y++;
                }
                foreach (var score in secondBestScores)
                {
                    if (IsUnknown(score))
                        continue;
                    int bin = Math.Min(secondBestBins.Count - 1, (int)((score - min) / binWidth));
                    secondBestBins[bin].Y++;
                }
            }

            hasUnknownScores = (countUnknownTargetScores + countUnknownDecoyScores + countUnknownSecondBestScores > 0);
            if (hasUnknownScores)
            {
                max += (max - min)*0.2;
                targetBins.Add(new PointPair(max, countUnknownTargetScores));
                decoyBins.Add(new PointPair(max, countUnknownDecoyScores));
                secondBestBins.Add(new PointPair(max, countUnknownSecondBestScores));
                max += binWidth;
            }
        }

        #endregion

        #region Grid support

        /// <summary>
        /// Disable calculators that produce unknown scores.
        /// </summary>
        private void OnDataBindingComplete(object sender, DataGridViewBindingCompleteEventArgs e)
        {
            var inactiveStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(230, 230, 230),
                    ForeColor = Color.FromArgb(100, 100, 100),
                    Font = new Font(gridPeakCalculators.Font, FontStyle.Italic)
                };
            for (int row = 0; row < gridPeakCalculators.RowCount; row++)
            {
                // Show row in disabled style if it contains a weight value of NaN.
                if (double.IsNaN(_targetDecoyGenerator.Weights[row]))
                {
                    for (int i = 0; i < 2; i++)
                    {
                        var cell = gridPeakCalculators.Rows[row].Cells[i];
                        cell.Style = inactiveStyle;
                        cell.ReadOnly = true;
                    }
                }
            }

            UpdateModelGraph();
        }

        /// <summary>
        /// Fill the data grid with calculator names and weights.
        /// </summary>
        private void InitializeCalculatorGrid()
        {
            // Create list of calculators and their corresponding weights.
            PeakCalculatorWeights.Clear();
            for (int i = 0; i < _peakScoringModel.PeakFeatureCalculators.Count; i++)
            {
                var name = _peakScoringModel.PeakFeatureCalculators[i].Name;
                double? weight = (PeakScoringModel.Weights == null || double.IsNaN(_peakScoringModel.Weights[i]))
                    ? (double?)null
                    : _peakScoringModel.Weights[i];
                PeakCalculatorWeights.Add(new PeakCalculatorWeight(name, weight));
            }
        }

        private void gridPeakCalculators_SelectionChanged(object sender, EventArgs e)
        {
            if (gridPeakCalculators.Rows.Count == 0)
                return;
            var row = gridPeakCalculators.SelectedCells.Count > 0 ? gridPeakCalculators.SelectedCells[0].RowIndex : 0;
            lblSelectedGraph.Text = gridPeakCalculators.Rows[row].Cells[0].Value.ToString();
            UpdateCalculatorGraph(row);
        }

        #endregion

        #region UI

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        private void btnTrainModel_Click(object sender, EventArgs e)
        {
            if (!decoyCheckBox.Checked && !secondBestCheckBox.Checked)
            {
                MessageDlg.Show(this, Resources.EditPeakScoringModelDlg_btnTrainModel_Click_Cannot_train_model_without_either_decoys_or_second_best_peaks_included_);
                return;
            }
            
            _peakScoringModel = (comboModel.SelectedIndex == MPROPHET_MODEL_INDEX)
                ? (IPeakScoringModel) new MProphetPeakScoringModel(ModelName)
                : new LegacyScoringModel(ModelName, double.NaN, double.NaN, decoyCheckBox.Checked, secondBestCheckBox.Checked);

            TrainModel();

            SetScoringModel(_peakScoringModel);   // update graphs and grid

            // We replace the original model when we retrain the mProphet model, because the list of
            // calculators may have changed.
            if (comboModel.SelectedIndex == MPROPHET_MODEL_INDEX)
                _originalPeakScoringModel = _peakScoringModel;
        }

        private void textMean_Leave(object sender, EventArgs e)
        {
            UpdateModelGraph();
        }

        private void textStdev_Leave(object sender, EventArgs e)
        {
            UpdateModelGraph();
        }

        private void comboModel_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_selectedIndex != comboModel.SelectedIndex)
            {
                _selectedIndex = comboModel.SelectedIndex;
                switch (comboModel.SelectedIndex)
                {
                    case MPROPHET_MODEL_INDEX:
                        SetScoringModel(_originalPeakScoringModel as MProphetPeakScoringModel); // default to mProphet
                        break;

                    case SKYLINE_LEGACY_MODEL_INDEX:
                        SetScoringModel(new LegacyScoringModel(ModelName, double.NaN, double.NaN));
                        break;
                }
            }
        }

        private string ModelName
        {
            get
            {
                var name = textName.Text.Trim();
                return name.Length > 0 ? name : UNNAMED;    // Not L10N
            }
            set { textName.Text = (value == UNNAMED) ? string.Empty : value; }
        }
        #endregion

        #region Functional test support

        public string PeakScoringModelName
        {
            get { return textName.Text; }
            set { textName.Text = value; }
        }

        public double? Mean
        {
            get { return string.IsNullOrEmpty(textMean.Text) ? (double?) null : double.Parse(textMean.Text); }
            set { textMean.Text = value.ToString(); }
        }

        public double? Stdev
        {
            get { return string.IsNullOrEmpty(textStdev.Text) ? (double?) null : double.Parse(textStdev.Text); }
            set { textStdev.Text = value.ToString(); }
        }

        public SimpleGridViewDriver<PeakCalculatorWeight> PeakCalculatorsGrid
        {
            get { return _gridViewDriver; }
        }

        #endregion

        /// <summary>
        /// Class to separate target and decoy peaks, and keep track of disabled calculators.
        /// </summary>
        private class TargetDecoyGenerator
        {
            private readonly PeakTransitionGroupFeatures[] _peakTransitionGroupFeaturesList;

            public double[] Weights { get; private set; }

            public TargetDecoyGenerator(IPeakScoringModel scoringModel)
            {
                // Determine which calculators will be used to score peaks in this document.
                var document = Program.ActiveDocumentUI;
                var calculators = scoringModel.PeakFeatureCalculators.ToArray();
                _peakTransitionGroupFeaturesList = document.GetPeakFeatures(calculators).ToArray();

                // Eliminate calculators/features that have no data for at least one of the transitions.
                Weights = new double[calculators.Length];
                for (int i = 0; i < Weights.Length; i++)
                {
                    // Disable calculators that have only a single score value or any unknown scores.
                    Weights[i] = GetInitialWeight(i, scoringModel.Weights != null ? scoringModel.Weights[i] : 0.0);
                }
            }

            public void GetTransitionGroups(out List<IList<double[]>> targetGroups,
                                            out List<IList<double[]>> decoyGroups)
            {
                targetGroups = new List<IList<double[]>>();
                decoyGroups = new List<IList<double[]>>();

                foreach (var peakTransitionGroupFeatures in _peakTransitionGroupFeaturesList)
                {
                    var transitionGroup = new List<double[]>();
                    foreach (var peakGroupFeatures in peakTransitionGroupFeatures.PeakGroupFeatures)
                        transitionGroup.Add(ToDoubles(peakGroupFeatures.Features));

                    if (peakTransitionGroupFeatures.Id.NodePep.IsDecoy)
                        decoyGroups.Add(transitionGroup);
                    else
                        targetGroups.Add(transitionGroup);
                }
            }

            /// <summary>
            /// Convert array of floats to array of doubles.
            /// </summary>
            private static double[] ToDoubles(float[] f)
            {
                var d = new double[f.Length];
                for (int i = 0; i < f.Length; i++)
                    d[i] = f[i];
                return d;
            }

            /// <summary>
            /// Calculate scores for targets and decoys.  A transition is selected from each transition group using the
            /// scoring weights, and then its score is calculated using the calculator weights applied to each feature.
            /// </summary>
            /// <param name="scoringWeights">Scoring coefficients for each feature.</param>
            /// <param name="calculatorWeights">Weighting coefficients for each feature.</param>
            /// <param name="targetScores">Output list of target scores.</param>
            /// <param name="decoyScores">Output list of decoy scores.</param>
            /// /// <param name="secondBestScores">Output list of false target scores.</param>
            public void GetScores(double[] scoringWeights, double[] calculatorWeights, out List<double> targetScores, out List<double> decoyScores,
                                  out List<double> secondBestScores)
            {
                targetScores = new List<double>();
                decoyScores = new List<double>();
                secondBestScores = new List<double>();

                foreach (var peakTransitionGroupFeatures in _peakTransitionGroupFeaturesList)
                {
                    PeakGroupFeatures maxFeatures = null;
                    PeakGroupFeatures nextFeatures = null;
                    double maxScore = double.MinValue;
                    double nextScore = double.MinValue;

                    // Find the highest and second highest scores among the transitions in this group.
                    foreach (var peakGroupFeatures in peakTransitionGroupFeatures.PeakGroupFeatures)
                    {
                        double score = GetScore(scoringWeights, peakGroupFeatures);
                        if (nextScore < score)
                        {
                            if (maxScore < score)
                            {
                                nextScore = maxScore;
                                maxScore = score;
                                nextFeatures = maxFeatures;
                                maxFeatures = peakGroupFeatures;
                            }
                            else
                            {
                                nextScore = score;
                                nextFeatures = peakGroupFeatures;
                            }
                        }
                    }

                    if (maxFeatures == null)
                        continue;   // No data

                    // If we have decoys, calculate a score and assign to the target or decoy list.
                    double currentScore = GetScore(calculatorWeights, maxFeatures);
                    if (peakTransitionGroupFeatures.Id.NodePep.IsDecoy)
                        decoyScores.Add(currentScore);
                    else
                    {
                        targetScores.Add(currentScore);
                        if (nextFeatures != null)
                            secondBestScores.Add(GetScore(calculatorWeights, nextFeatures));
                    }
                }
            }

            /// <summary>
            /// Calculate scores for targets and decoys.  A transition is selected from each transition group using the
            /// scoring weights, and then its score is calculated using the calculator weights applied to each feature.
            /// </summary>
            private double GetInitialWeight(int calculatorIndex, double weightFromModel)
            {
                double maxValue = double.MinValue;
                double minValue = double.MaxValue;

                foreach (var peakTransitionGroupFeatures in _peakTransitionGroupFeaturesList)
                {
                    // Find the highest and second highest scores among the transitions in this group.
                    foreach (var peakGroupFeatures in peakTransitionGroupFeatures.PeakGroupFeatures)
                    {
                        double value = peakGroupFeatures.Features[calculatorIndex];
                        if (IsUnknown(value))
                            return double.NaN;
                        maxValue = Math.Max(value, maxValue);
                        minValue = Math.Min(value, minValue);
                    }
                }

                return (maxValue > minValue) ? weightFromModel : double.NaN;
            }

            /// <summary>
            /// Calculate the score of a set of features given an array of weighting coefficients.
            /// </summary>
            private static double GetScore(double[] weights, PeakGroupFeatures peakGroupFeatures)
            {
                double score = 0;
                for (int i = 0; i < weights.Length; i++)
                {
                    if (!double.IsNaN(weights[i]))
                        score += weights[i] * peakGroupFeatures.Features[i];
                }
                return score;
            }
        }

        private class PeakCalculatorGridViewDriver : SimpleGridViewDriver<PeakCalculatorWeight>
        {
            public PeakCalculatorGridViewDriver(DataGridViewEx gridView,
                                             BindingSource bindingSource,
                                             SortableBindingList<PeakCalculatorWeight> items)
                : base(gridView, bindingSource, items)
            {
                var calculators = PeakFeatureCalculator.Calculators.ToArray();
                for (int i = 0; i < calculators.Length; i++) 
                    Items.Add(new PeakCalculatorWeight(calculators[i].Name, null));
            }

            protected override void DoPaste()
            {
                // No pasting.
            }
        }

        private void zedGraph_ContextMenuBuilder(ZedGraphControl graphControl, ContextMenuStrip menuStrip, Point mousePt, ZedGraphControl.ContextMenuObjectState objState)
        {
            var items = new ToolStripItem[menuStrip.Items.Count];
            for (int i = 0; i < items.Length; i++)
                items[i] = menuStrip.Items[i];

            // Remove some ZedGraph menu items not of interest
            foreach (var item in items)
            {
                var tag = (string)item.Tag;
                if (tag == "set_default" || tag == "show_val" || tag == "unzoom" || tag == "undo_all") // Not L10N
                    menuStrip.Items.Remove(item);
            }
            CopyEmfToolStripMenuItem.AddToContextMenu(graphControl, menuStrip);
        }

        private void decoyCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            UpdateCalculatorGraph();
            UpdateModelGraph();
        }

        private void falseTargetCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            UpdateCalculatorGraph();
            UpdateModelGraph();
        }
    }

    /// <summary>
    /// Associate a weight value with a calculator for display in the grid.
    /// </summary>
    public class PeakCalculatorWeight
    {
        public string Name { get; private set; }
        public double? Weight { get; set; }

        public PeakCalculatorWeight(string name, double? weight)
        {
            Name = name;
            Weight = weight;
        }
    }
}
