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
using System.IO;
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

        private readonly IEnumerable<IPeakScoringModel> _existing;
        private MProphetPeakScoringModel _peakScoringModelNew;
        private readonly TargetDecoyGenerator _targetDecoyGenerator;
        private readonly PeakCalculatorGridViewDriver _gridViewDriver;
        private int _selectedCalculator = -1;
        private SortableBindingList<PeakCalculatorWeight> PeakCalculatorWeights { get { return _gridViewDriver.Items; } }

        /// <summary>
        /// Create a new scoring model, or edit an existing model.
        /// </summary>
        /// <param name="model">Null to create a new model</param>
        /// <param name="existing">Existing scoring models (to check for unique name)</param>
        public EditPeakScoringModelDlg(MProphetPeakScoringModel model, IEnumerable<IPeakScoringModel> existing)
        {
            InitializeComponent();

            Icon = Resources.Skyline;

            _gridViewDriver = new PeakCalculatorGridViewDriver(gridPeakCalculators, bindingPeakCalculators,
                                                            new SortableBindingList<PeakCalculatorWeight>());
            gridPeakCalculators.CellValueChanged += DataValueChanged;
            gridPeakCalculators.DataBindingComplete += OnDataBindingComplete;
            textMean.TextChanged += DataValueChanged;
            textStdev.TextChanged += DataValueChanged;

            _targetDecoyGenerator = new TargetDecoyGenerator();

            lblColinearWarning.Visible = false;
            if (model != null)
            {
                _peakScoringModelNew = PeakScoringModel = model;
                textName.Text = PeakScoringModel.Name;
                textMean.Text = PeakScoringModel.DecoyMean.ToString("F3"); // Not L10N
                textStdev.Text = PeakScoringModel.DecoyStdev.ToString("F3"); // Not L10N
                lblColinearWarning.Visible = _peakScoringModelNew.ColinearWarning;
                InitializeCalculatorGrid();
            }

            _existing = existing;

            InitGraphPane(zedGraphMProphet.GraphPane);
            InitGraphPane(zedGraphSelectedCalculator.GraphPane);
        }

        /// <summary>
        /// Get/set the peak scoring model.
        /// </summary>
        public MProphetPeakScoringModel PeakScoringModel { get; private set; }

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

            // Create new scoring model using the active calculators.
            var modelName = _peakScoringModelNew != null ? _peakScoringModelNew.Name : "~"; // Not L10N
            _peakScoringModelNew = new MProphetPeakScoringModel(modelName, PeakFeatureCalculator.CalculatorTypes, _targetDecoyGenerator.Weights);  

            // Train the model.
            _peakScoringModelNew = (MProphetPeakScoringModel)_peakScoringModelNew.Train(targetTransitionGroups, decoyTransitionGroups);

            // Copy weights to grid.
            for (int i = 0; i < _gridViewDriver.Items.Count; i++)
            {
                double weight = _peakScoringModelNew.Weights[i];
                _gridViewDriver.Items[i].Weight = double.IsNaN(weight) ? null : (double?) weight;
            }
            gridPeakCalculators.Invalidate();

            // Copy mean and stdev to text boxes.
            textMean.Text = _peakScoringModelNew.DecoyMean.ToString("0.000");
            textStdev.Text = _peakScoringModelNew.DecoyStdev.ToString("0.000");
            lblColinearWarning.Visible = _peakScoringModelNew.ColinearWarning;

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

            if (_existing != null && _existing.Contains(r => !ReferenceEquals(PeakScoringModel, r) && Equals(name, r.Name)))
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

            if (_peakScoringModelNew == null)
            {
                MessageDlg.Show(this, Resources.EditPeakScoringModelDlg_OkDialog_The_model_must_be_trained_first_);
                return;
            }

            PeakScoringModel = new MProphetPeakScoringModel(
                name,
                _peakScoringModelNew.PeakFeatureCalculators,
                _peakScoringModelNew.Weights,
                decoyMean,
                decoyStdev,
                _peakScoringModelNew.ColinearWarning);

            DialogResult = DialogResult.OK;
        }

        private static bool IsUnknown(double d)
        {
            return (double.IsNaN(d) || double.IsInfinity(d));
        }

        /// <summary>
        /// Create weights array.  The given index will have a value of 1, all the others
        /// will have a value of NaN.
        /// </summary>
        private static double[] CreateWeightsSelect(int index)
        {
            var weights = new double[PeakFeatureCalculator.CountCalculators];
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
        private void InitGraphPane(GraphPane graphPane)
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
            btnUpdateGraphs.Enabled = false;

            var graphPane = zedGraphMProphet.GraphPane;
            graphPane.CurveList.Clear();
            graphPane.GraphObjList.Clear();

            // Nothing to draw if we don't have a model yet.
            if (_peakScoringModelNew == null)
                return;

            // Get binned scores for targets and decoys.
            PointPairList targetPoints;
            PointPairList decoyPoints;
            double min;
            double max;
            bool hasUnknownScores;
            GetPoints(
                -1,
                out targetPoints,
                out decoyPoints,
                out min,
                out max,
                out hasUnknownScores);

            // Add bar graphs.
            graphPane.AddBar(Resources.EditPeakScoringModelDlg_UpdateModelGraph_Decoys, decoyPoints, _decoyColor);
            graphPane.AddBar(Resources.EditPeakScoringModelDlg_UpdateModelGraph_Targets, targetPoints, _targetColor);

            // Graph normal curve if we have values for the decoy mean and stdev.
            double decoyMean;
            double decoyStdev;
            if (double.TryParse(textMean.Text, out decoyMean) &&
                double.TryParse(textStdev.Text, out decoyStdev))
            {
                // Calculate scale value for normal curve by calculating area of decoy histogram.
                double yScale = 0;
                double previousY = 0;
                foreach (var decoyPoint in decoyPoints)
                {
                    // Add trapezoidal area.
                    yScale += Math.Max(previousY, decoyPoint.Y) - Math.Abs(previousY - decoyPoint.Y)/2;
                    previousY = decoyPoint.Y;
                }
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
                var curve = new LineItem(Resources.EditPeakScoringModelDlg_UpdateModelGraph_Decoy_norm, normalCurve, Color.DarkGray, SymbolType.None)
                    {
                        Line =
                            {
                                Width = 2, 
                                Fill = new Fill(Color.FromArgb(255, 230, 195)), 
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
            double min;
            double max;
            bool hasUnknownScores;
            GetPoints(
                _selectedCalculator,
                out targetPoints,
                out decoyPoints, 
                out min, 
                out max,
                out hasUnknownScores);

            graphPane.AddBar(Resources.EditPeakScoringModelDlg_UpdateModelGraph_Decoys, decoyPoints, _decoyColor);
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
        /// <param name="min">Min score.</param>
        /// <param name="max">Max score.</param>
        /// <param name="hasUnknownScores">Returns true if the calculator returns unknown scores.</param>
        private void GetPoints(
            int selectedCalculator, 
            out PointPairList targetBins, out PointPairList decoyBins,
            out double min, out double max,
            out bool hasUnknownScores)
        {
            double[] scoringWeights;
            double[] calculatorWeights;
            if (selectedCalculator == -1)
            {
               scoringWeights =  calculatorWeights = _peakScoringModelNew.Weights.ToArray();
            }
            else
            {
                scoringWeights = CreateWeightsSelect(0);
                calculatorWeights = CreateWeightsSelect(selectedCalculator);
            }

            List<double> targetScores;
            List<double> decoyScores;
            _targetDecoyGenerator.GetScores(scoringWeights, calculatorWeights, out targetScores, out decoyScores);
            
            // Find score range for targets and decoys
            min = float.MaxValue;
            max = float.MinValue;
            int countUnknownTargetScores = 0;
            int countUnknownDecoyScores = 0;
            foreach (var score in targetScores)
                ProcessScores(score, ref min, ref max, ref countUnknownTargetScores);
            foreach (var score in decoyScores)
                ProcessScores(score, ref min, ref max, ref countUnknownDecoyScores);

            // Sort scores into bins.
            targetBins = new PointPairList();
            decoyBins = new PointPairList();
            double binWidth = (max - min)/HISTOGRAM_BAR_COUNT;

            if (countUnknownTargetScores == targetScores.Count &&
                countUnknownDecoyScores == decoyScores.Count)
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
            }

            hasUnknownScores = (countUnknownTargetScores + countUnknownDecoyScores > 0);
            if (hasUnknownScores)
            {
                max += (max - min)*0.2;
                targetBins.Add(new PointPair(max, countUnknownTargetScores));
                decoyBins.Add(new PointPair(max, countUnknownDecoyScores));
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
        /// Enable button to update graphs if any calculator has a defined weight.
        /// </summary>
        private void DataValueChanged(object sender, EventArgs e)
        {
            for (int i = 0; i < _gridViewDriver.Items.Count; i++)
            {
                if (_gridViewDriver.Items[i].Weight.HasValue)
                {
                    btnUpdateGraphs.Enabled = true;
                    break;
                }
            }
        }

        /// <summary>
        /// Fill the data grid with calculator names and weights.
        /// </summary>
        private void InitializeCalculatorGrid()
        {
            // Create list of calculators and their corresponding weights.  If no scoring model
            // exists yet, the weights will be null.
            PeakCalculatorWeights.Clear();
            foreach (var calculator in PeakFeatureCalculator.Calculators)
            {
                for (int i = 0; i < _peakScoringModelNew.PeakFeatureCalculators.Count; i++)
                {
                    if (calculator.GetType() == _peakScoringModelNew.PeakFeatureCalculators[i])
                    {
                        var weight = _peakScoringModelNew.Weights[i];
                        PeakCalculatorWeights.Add(new PeakCalculatorWeight(calculator.Name,
                            double.IsNaN(weight) ? null : (double?) weight));
                        break;
                    }
                }
            }
        }

        private void gridPeakCalculators_SelectionChanged(object sender, EventArgs e)
        {
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
            TrainModel();
        }

        private void btnUpdateGraphs_Click(object sender, EventArgs e)
        {
            UpdateModelGraph();
            UpdateCalculatorGraph();
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
            private readonly bool _hasDecoys;

            public double[] Weights { get; private set; }

            public TargetDecoyGenerator()
            {
                // Determine which calculators will be used to score peaks in this document.
                var document = Program.ActiveDocumentUI;
                _peakTransitionGroupFeaturesList = document.GetPeakFeatures(PeakFeatureCalculator.Calculators.ToArray()).ToArray();

                // Determine if this document contains decoys.
                foreach (var peakTransitionGroupFeatures in _peakTransitionGroupFeaturesList)
                {
                    if (peakTransitionGroupFeatures.Id.NodePep.IsDecoy)
                    {
                        _hasDecoys = true;
                        break;
                    }
                }

                // Eliminate calculators/features that have no data for at least one of the transitions.
                Weights = new double[PeakFeatureCalculator.CountCalculators];
                for (int i = 0; i < Weights.Length; i++)
                {
                    // Disable calculators that have only a single score value or any unknown scores.
                    Weights[i] = GetInitialWeight(i);
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
            public void GetScores(double[] scoringWeights, double[] calculatorWeights, out List<double> targetScores, out List<double> decoyScores)
            {
                targetScores = new List<double>();
                decoyScores = new List<double>();

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
                    if (_hasDecoys)
                    {
                        double score = GetScore(calculatorWeights, maxFeatures);
                        if (peakTransitionGroupFeatures.Id.NodePep.IsDecoy)
                            decoyScores.Add(score);
                        else
                            targetScores.Add(score);
                    }

                    // If we don't have decoys, we generate a decoy score from the second-highest scoring
                    // transition in the group.
                    else
                    {
                        targetScores.Add(GetScore(calculatorWeights, maxFeatures));
                        if (nextFeatures != null)
                            decoyScores.Add(GetScore(calculatorWeights, nextFeatures));
                    }
                }
            }

            /// <summary>
            /// Calculate scores for targets and decoys.  A transition is selected from each transition group using the
            /// scoring weights, and then its score is calculated using the calculator weights applied to each feature.
            /// </summary>
            private double GetInitialWeight(int calculatorIndex)
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

                return (maxValue > minValue) ? 0 : double.NaN;
            }

            /// <summary>
            /// Calculate the score of a set of features given an array of weighting coefficients.
            /// </summary>
            private double GetScore(double[] weights, PeakGroupFeatures peakGroupFeatures)
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
            Validate();
        }

        public void Validate()
        {
            if (!PeakFeatureCalculator.Calculators.Any(calculator => Matches(calculator.Name)))
                throw new InvalidDataException(string.Format(Resources.PeakCalculatorWeight_Validate___0___is_not_a_known_name_for_a_peak_feature_calculator, Name));
        }

        public bool Matches(string name)
        {
            return (String.Compare(name, Name, StringComparison.OrdinalIgnoreCase) == 0);
        }
    }
}
