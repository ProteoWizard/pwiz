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
using pwiz.Skyline.Model.Find;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

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
        private IPeakScoringModel _lastTrainedScoringModel;
        private const string UNNAMED = "<UNNAMED>"; // Not L10N
        private string _originalName;
        private TargetDecoyGenerator _targetDecoyGenerator;
        private readonly PeakCalculatorGridViewDriver _gridViewDriver;
        private int _selectedCalculator = -1;
        private int _selectedIndex = -1;
        private bool _hasUnknownScores;
        private bool _allUnknownScores;
        private SortableBindingList<PeakCalculatorWeight> PeakCalculatorWeights { get { return _gridViewDriver.Items; } }
        
        private enum ColumnNames { enabled, calculator_name, weight, percent_contribution }

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

            InitGraphPane(zedGraphMProphet.GraphPane, Resources.EditPeakScoringModelDlg_EditPeakScoringModelDlg_Train_a_model_to_see_composite_score);
            InitGraphPane(zedGraphSelectedCalculator.GraphPane);

            lblColinearWarning.Visible = false;
            toolStripFind.Visible = false;

            gridPeakCalculators.DataBindingComplete += OnDataBindingComplete;
            comboModel.SelectedIndexChanged += comboModel_SelectedIndexChanged;
        }

        /// <summary>
        /// Get/set the peak scoring model.
        /// </summary>
        public IPeakScoringModel PeakScoringModel
        {
            get { return _peakScoringModel; }
            set
            {
                // Scoring model is null if we're creating a new model from scratch (default to mProphet).
                SetScoringModel(value ?? new MProphetPeakScoringModel(UNNAMED));
            }
        }

        private void SetScoringModel(IPeakScoringModel scoringModel)
        {
            _peakScoringModel = scoringModel;
            ModelName = _peakScoringModel.Name;
            if (_originalName == null)
                _originalName = _peakScoringModel.Name;

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

            if (_lastTrainedScoringModel == null)
                _lastTrainedScoringModel = _peakScoringModel;

            decoyCheckBox.Checked = _peakScoringModel.UsesDecoys;
            secondBestCheckBox.Checked = _peakScoringModel.UsesSecondBest;
            UpdateCalculatorGraph(0);
            UpdateModelGraph();
        }

        /// <summary>
        /// Get the number format used for displaying weights in calculator grid.
        /// </summary>
        public string PeakCalculatorWeightFormat { get { return gridPeakCalculators.Columns[(int) ColumnNames.weight].DefaultCellStyle.Format; } }

        public string PeakCalculatorPercentContributionFormat 
        { 
            get
            {
                return gridPeakCalculators.Columns[(int) ColumnNames.percent_contribution].DefaultCellStyle.Format;
            } 
        }

        public void TrainModelClick()
        {
            if (!decoyCheckBox.Checked && !secondBestCheckBox.Checked)
            {
                MessageDlg.Show(this, Resources.EditPeakScoringModelDlg_btnTrainModel_Click_Cannot_train_model_without_either_decoys_or_second_best_peaks_included_);
                return;
            }

            try
            {
                TrainModel(true);
                SetScoringModel(_peakScoringModel);   // update graphs and grid
            }
            catch (InvalidDataException x)
            {
                MessageDlg.Show(this,
                               TextUtil.LineSeparate(string.Format(Resources.EditPeakScoringModelDlg_btnTrainModel_Click_Failed_training_the_model_),
                                                     x.Message));
            }

            // Update last trained scoring model.
            if (comboModel.SelectedIndex == MPROPHET_MODEL_INDEX)
                _lastTrainedScoringModel = _peakScoringModel;
        }

        /// <summary>
        /// Train mProphet scoring model. If suppressWeights is true, disable the weights unchecked by the user
        /// </summary>
        public void TrainModel(bool suppressWeights = false)
        {
            // Create new scoring model using the default calculators.
            switch (comboModel.SelectedIndex)
            {
                case MPROPHET_MODEL_INDEX:
                    _peakScoringModel = new MProphetPeakScoringModel(ModelName, null as LinearModelParams, null, decoyCheckBox.Checked, secondBestCheckBox.Checked);  
                    break;
                case SKYLINE_LEGACY_MODEL_INDEX:
                    _peakScoringModel = new LegacyScoringModel(ModelName, null, decoyCheckBox.Checked, secondBestCheckBox.Checked);
                    break;
            }

            // Disable the calculator types that were unchecked
            var indicesToSuppress = _gridViewDriver.Items.Select(item => !item.IsEnabled).ToList();
            var calculatorsToSuppress = _targetDecoyGenerator.FeatureCalculators.Select(calc => calc.GetType())
                                                             .Where((type, i) => indicesToSuppress[i])
                                                             .ToList();
            var weightSuppressors =
                _peakScoringModel.PeakFeatureCalculators.Select(calc => suppressWeights && calculatorsToSuppress.Contains(calc.GetType())).ToList();


            // Need to regenerate the targets and decoys if the set of calculators has changed (backward compatibility)
            if (!PeakScoringModelSpec.AreSameCalculators(_peakScoringModel.PeakFeatureCalculators, 
                                                         _targetDecoyGenerator.FeatureCalculators))
            {
                SetScoringModel(_peakScoringModel);
            }

            // Get scores for target and decoy groups.
            List<IList<double[]>> targetTransitionGroups;
            List<IList<double[]>> decoyTransitionGroups;
            _targetDecoyGenerator.GetTransitionGroups(out targetTransitionGroups, out decoyTransitionGroups);
            // If decoy box is checked and no decoys, throw an error
            if (decoyCheckBox.Checked && decoyTransitionGroups.Count == 0)
                throw new InvalidDataException(string.Format(Resources.EditPeakScoringModelDlg_TrainModel_There_are_no_decoy_peptides_in_the_current_document__Uncheck_the_Use_Decoys_Box_));
            // Use decoys for training only if decoy box is checked
            if (!decoyCheckBox.Checked)
                decoyTransitionGroups = new List<IList<double[]>>();

            // Set intial weights based on previous model (with NaN's reset to 0)
            var initialWeights = new double[_peakScoringModel.PeakFeatureCalculators.Count];
            // But then set to NaN the weights that were suppressed by the user or have unknown values for this dataset
            for (int i = 0; i < initialWeights.Length; ++i)
            {
                if (!_targetDecoyGenerator.EligibleScores[i] || weightSuppressors[i])
                    initialWeights[i] = double.NaN;
            }
            var initialParams = new LinearModelParams(initialWeights);

            // Train the model.
            _peakScoringModel = _peakScoringModel.Train(targetTransitionGroups, decoyTransitionGroups, initialParams, secondBestCheckBox.Checked);

            // Copy weights to grid.
            for (int i = 0; i < _gridViewDriver.Items.Count; i++)
            {
                double weight = _peakScoringModel.Parameters.Weights[i];
                _gridViewDriver.Items[i].Weight = double.IsNaN(weight) ? null : (double?) weight;
                _gridViewDriver.Items[i].PercentContribution = double.IsNaN(weight) ? null : GetPercentContribution(i);
            }
            gridPeakCalculators.Invalidate();

            lblColinearWarning.Visible = _peakScoringModel is MProphetPeakScoringModel && ((MProphetPeakScoringModel)_peakScoringModel).ColinearWarning;

            UpdateCalculatorGraph();
            UpdateModelGraph();
            UpdateCalculatorGrid();
        }

        public void FindMissingValues(int selectedCalculatorIndex)
        {
            string calculatorName = _peakScoringModel.PeakFeatureCalculators[selectedCalculatorIndex].Name;
            var featureDictionary = _targetDecoyGenerator.PeakTransitionGroupDictionary;
            var finders = new[]
                {
                    new MissingScoresFinder(calculatorName, selectedCalculatorIndex, featureDictionary)
                };
            var findOptions = new FindOptions().ChangeCustomFinders(finders).ChangeCaseSensitive(false).ChangeText("");
            Program.MainWindow.FindAll(this, findOptions);
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

            DialogResult = DialogResult.OK;

            var mProphetModel = _peakScoringModel as MProphetPeakScoringModel;
            if (mProphetModel != null)
            {
                _peakScoringModel = new MProphetPeakScoringModel(
                    name,
                    mProphetModel.Parameters,
                    mProphetModel.PeakFeatureCalculators,
                    mProphetModel.UsesDecoys,
                    mProphetModel.UsesSecondBest,
                    mProphetModel.ColinearWarning);
                return;
            }

            var legacyModel = _peakScoringModel as LegacyScoringModel;
            if (legacyModel != null)
            {
                _peakScoringModel = new LegacyScoringModel(
                    name,
                    legacyModel.Parameters, 
                    legacyModel.UsesDecoys,
                    legacyModel.UsesSecondBest);
            }
        }

        public static bool IsUnknown(double d)
        {
            return (double.IsNaN(d) || double.IsInfinity(d));
        }

        /// <summary>
        /// Has this weight been assigned a score opposite to that expected in its definition?
        /// </summary>
        /// <param name="index"> Index of the weight </param>
        /// <returns></returns>
        private bool IsWrongSignWeight(int index)
        {
            if (_peakScoringModel.Parameters == null || _peakScoringModel.Parameters.Weights == null)
                return false;
            double weight = _peakScoringModel.Parameters.Weights[index];
            if (double.IsNaN(weight))
                return false;
            return _peakScoringModel.PeakFeatureCalculators[index].IsReversedScore ^ (weight < 0);
        }

        /// <summary>
        /// Create parameter object in which the given index will have a value of 1, all the others
        /// will have a value of NaN, and the bias is zero.
        /// </summary>
        private LinearModelParams CreateParametersSelect(int index)
        {
            var weights = new double[_peakScoringModel.PeakFeatureCalculators.Count];
            for (int i = 0; i < weights.Length; i++)
                weights[i] = double.NaN;
            weights[index] = 1;
            return new LinearModelParams(weights);
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
        private static void InitGraphPane(GraphPane graphPane, string titleName = null)
        {
            graphPane.Title.Text = titleName ?? "";
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
            if (PeakScoringModel.Parameters == null || PeakScoringModel.Parameters.Weights == null)
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
            bool allUnknownScores;
            GetPoints(
                -1,
                out targetPoints,
                out decoyPoints,
                out secondBestPoints,
                out min,
                out max,
                out hasUnknownScores,
                out allUnknownScores);

            // If there are unknown composite scores, display the model graph but warn the user
            // that the model does not apply to this data and the model should be retrained
            graphPane.Title.Text = hasUnknownScores ? 
                                                    Resources.EditPeakScoringModelDlg_UpdateModelGraph_Trained_model_is_not_applicable_to_current_dataset_: 
                                                    Resources.EditPeakScoringModelDlg_EditPeakScoringModelDlg_Composite_Score__Normalized_;

            // Add bar graphs.
            if (decoyCheckBox.Checked)
                graphPane.AddBar(Resources.EditPeakScoringModelDlg_UpdateModelGraph_Decoys, decoyPoints, _decoyColor);
            if (secondBestCheckBox.Checked)
                graphPane.AddBar(Resources.EditPeakScoringModelDlg_UpdateCalculatorGraph_Second_Best_Peaks, secondBestPoints, _secondBestColor);
            graphPane.AddBar(Resources.EditPeakScoringModelDlg_UpdateModelGraph_Targets, targetPoints, _targetColor);

            // Graph normal curve if the model is trained and at least some composite scores are valid
            if (_peakScoringModel.Parameters != null && !allUnknownScores)
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
                min = Math.Min(min, -4);
                max = Math.Max(max, 4);

                // Calculate points on the normal curve.
                PointPairList normalCurve = new PointPairList();
                double xStep = binWidth/8;
                for (double x = min; x < max; x += xStep)
                {
                    double norm = Statistics.DNorm(x);
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

            // If the calculator produces unknown scores, split the graph and show a histogram
            // of the unknown scores on the right side of the graph.
            // Unlike the calculator graph, the model graph should ONLY produce unknown scores when a model is applied to an incompatible dataset
            if (hasUnknownScores)
                CreateUnknownLabel(graphPane);

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
            GetPoints(
                _selectedCalculator,
                out targetPoints,
                out decoyPoints,
                out secondBestPoints,
                out min, 
                out max,
                out _hasUnknownScores,
                out _allUnknownScores);
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
            if (_hasUnknownScores)
                CreateUnknownLabel(graphPane);

            // Change the graph title
            if(_selectedCalculator >= 0)
                graphPane.Title.Text = gridPeakCalculators.Rows[_selectedCalculator].Cells[(int)ColumnNames.calculator_name].Value.ToString();

            // Calculate the Axis Scale Ranges
            graphPane.AxisChange();
            zedGraphSelectedCalculator.Refresh();
        }

        private static void CreateUnknownLabel(GraphPane graphPane)
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
        /// <param name="allUnknownScores">Returns true if all scores in the calculator are unknown.</param>
        private void GetPoints(
            int selectedCalculator, 
            out PointPairList targetBins, 
            out PointPairList decoyBins,
            out PointPairList secondBestBins,
            out double min, 
            out double max,
            out bool hasUnknownScores,
            out bool allUnknownScores)
        {
            var modelParameters = _peakScoringModel.IsTrained ? _peakScoringModel.Parameters : new LinearModelParams(_peakScoringModel.PeakFeatureCalculators.Count);
            LinearModelParams calculatorParameters = selectedCalculator == -1 ? modelParameters : CreateParametersSelect(selectedCalculator);

            List<double> targetScores;
            List<double> decoyScores;
            List<double> secondBestScores;
            // Invert the score if its "natural" sign as specified in the calculator's definition is negative
            bool invert = selectedCalculator != -1 && _peakScoringModel.PeakFeatureCalculators[selectedCalculator].IsReversedScore;
            // Evaluate each score on the best peak according to that score (either individual calculator or composite)
            _targetDecoyGenerator.GetScores(calculatorParameters, calculatorParameters, out targetScores, out decoyScores, out secondBestScores, invert);
            
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

            allUnknownScores = countUnknownTargetScores == targetScores.Count &&
                               countUnknownDecoyScores == decoyScores.Count &&
                               countUnknownSecondBestScores == secondBestScores.Count;
            if (allUnknownScores)
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
            UpdateCalculatorGrid();
        }

        private void UpdateCalculatorGrid()
        {
            var inactiveStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(230, 230, 230),
                    ForeColor = Color.FromArgb(100, 100, 100),
                    Font = new Font(gridPeakCalculators.Font, FontStyle.Italic)
                };
            var warningStyle = new DataGridViewCellStyle
                {
                    ForeColor = Color.Red
                };

            // The score used for bootstrap cannot be disabled
            if (gridPeakCalculators.RowCount > 0)
            {
                var bootstrapCell = gridPeakCalculators.Rows[0].Cells[(int) ColumnNames.enabled];
                bootstrapCell.ReadOnly = true;
                bootstrapCell.Style = inactiveStyle;
            }
            for (int row = 0; row < gridPeakCalculators.RowCount; row++)
            {
                // Show row in red if weight is the wrong sign
                if (IsWrongSignWeight(row))
                {
                    for (int i = 0; i < 4; i++)
                    {
                        var cell = gridPeakCalculators.Rows[row].Cells[i];
                        cell.Style = warningStyle;
                        cell.ToolTipText = Resources.EditPeakScoringModelDlg_OnDataBindingComplete_Unexpected_Coefficient_Sign;
                    }
                }
                // Show row in disabled style if the score is not eligible
                if (!_targetDecoyGenerator.EligibleScores[row])
                {
                    for (int i = 0; i < 4; i++)
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
                bool isNanWeight = !_peakScoringModel.IsTrained ||
                                   double.IsNaN(_peakScoringModel.Parameters.Weights[i]);

                var name = _peakScoringModel.PeakFeatureCalculators[i].Name;
                double? weight = null, normalWeight = null;
                if (!isNanWeight)
                {
                    weight = _peakScoringModel.Parameters.Weights[i];
                    normalWeight = GetPercentContribution(i);
                }
                // If the score is not eligible (e.g. has unknown values), definitely don't enable it
                // If it is eligible, enable if untrained or if trained and not nan
                bool enabled = _targetDecoyGenerator.EligibleScores[i] && 
                              (!_peakScoringModel.IsTrained || !double.IsNaN(_peakScoringModel.Parameters.Weights[i]));
                PeakCalculatorWeights.Add(new PeakCalculatorWeight(name, weight, normalWeight, enabled));
            }
        }

        private double? GetPercentContribution(int index)
        {
            if (double.IsNaN(_peakScoringModel.Parameters.Weights[index]))
                return null;
            List<double> targetScores;
            List<double> activeDecoyScores;
            List<double> targetScoresAll;
            List<double> activeDecoyScoresAll;
            var scoringParameters = _peakScoringModel.Parameters;
            var calculatorParameters = CreateParametersSelect(index);
            GetActiveScoredValues(scoringParameters, calculatorParameters, out targetScores, out activeDecoyScores);
            GetActiveScoredValues(scoringParameters, scoringParameters, out targetScoresAll, out activeDecoyScoresAll);
            if (targetScores.Count == 0 ||
                activeDecoyScores.Count == 0 ||
                targetScoresAll.Count == 0 ||
                activeDecoyScoresAll.Count == 0)
            {
                return null;
            }
            double meanDiffAll = targetScoresAll.Average() - activeDecoyScoresAll.Average();
            double meanDiff = targetScores.Average() - activeDecoyScores.Average();
            double meanWeightedDiff = meanDiff * _peakScoringModel.Parameters.Weights[index];
            if (meanDiffAll == 0 || double.IsNaN(meanDiffAll) || double.IsNaN(meanDiff))
                return null;
            return meanWeightedDiff / meanDiffAll;
        }

        private void GetActiveScoredValues(LinearModelParams scoringParams, 
                                                    LinearModelParams calculatorParams,
                                                    out List<double> targetScores,
                                                    out List<double> activeDecoyScores)
        {
            List<double> decoyScores;
            List<double> secondBestScores;
            activeDecoyScores = new List<double>();
            _targetDecoyGenerator.GetScores(scoringParams,
                                calculatorParams,
                                out targetScores,
                                out decoyScores,
                                out secondBestScores);
            if (_peakScoringModel.UsesDecoys)
                activeDecoyScores.AddRange(decoyScores);
            if (_peakScoringModel.UsesSecondBest)
                activeDecoyScores.AddRange(secondBestScores);
        }

        private void gridPeakCalculators_SelectionChanged(object sender, EventArgs e)
        {
            if (gridPeakCalculators.Rows.Count == 0 || _peakScoringModel == null)
                return;
            var row = gridPeakCalculators.SelectedCells.Count > 0 ? gridPeakCalculators.SelectedCells[0].RowIndex : 0;
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
            TrainModelClick();
        }

        private void comboModel_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_selectedIndex != comboModel.SelectedIndex)
            {
                _selectedIndex = comboModel.SelectedIndex;
                switch (comboModel.SelectedIndex)
                {
                    case MPROPHET_MODEL_INDEX:
                        SetScoringModel(_lastTrainedScoringModel is MProphetPeakScoringModel
                                            ? _lastTrainedScoringModel
                                            : new MProphetPeakScoringModel(ModelName));
                        break;

                    case SKYLINE_LEGACY_MODEL_INDEX:
                        SetScoringModel(_lastTrainedScoringModel is LegacyScoringModel
                                            ? _lastTrainedScoringModel
                                            : new LegacyScoringModel(ModelName));
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

        private void findPeptidesButton_Click(object sender, EventArgs e)
        {
            FindMissingValues(_selectedCalculator);
        }

        private bool zedGraphSelectedCalculator_MouseMoveEvent(ZedGraphControl sender, MouseEventArgs e)
        {
            // Find button is visible on the right side of graph, if some scores are unknown, but not if all scores are unknown
            bool isMouseOverUnknown = _hasUnknownScores && !_allUnknownScores && e.X > sender.Width - 100;
            toolStripFind.Visible = isMouseOverUnknown;
            return true;
        }
        #endregion

        #region Functional test support

        public string PeakScoringModelName
        {
            get { return textName.Text; }
            set { textName.Text = value; }
        }

        public bool UsesDecoys
        {
            get { return decoyCheckBox.Checked; }
            set { decoyCheckBox.Checked = value; }
        }

        public bool UsesSecondBest
        {
            get { return secondBestCheckBox.Checked; }
            set { secondBestCheckBox.Checked = value; }
        }

        public string SelectedModelItem
        {
            get { return comboModel.SelectedItem.ToString(); }
            set
            {
                foreach (var item in comboModel.Items)
                {
                    if (item.ToString() == value)
                    {
                        comboModel.SelectedItem = item;
                        return;
                    }
                }
                throw new InvalidDataException(Resources.EditPeakScoringModelDlg_SelectedModelItem_Invalid_Model_Selection);
            }
        }

        public int SelectedGraphTab
        {
            get { return tabControl1.SelectedIndex; }
            set { tabControl1.SelectTab(value); }
        }

        public SimpleGridViewDriver<PeakCalculatorWeight> PeakCalculatorsGrid
        {
            get { return _gridViewDriver; }
        }

        public bool IsActiveCell(int row, int column)
        {
            return !gridPeakCalculators.Rows[row].Cells[column].ReadOnly;
        }


        public void SetChecked(int row, bool value)
        {
            _gridViewDriver.Items[row].IsEnabled = value;
        }

        public bool IsFindButtonVisible
        {
            get { return toolStripFind.Visible; }
            set { toolStripFind.Visible = value; }
        }

        #endregion

        /// <summary>
        /// Class to separate target and decoy peaks, and keep track of disabled calculators.
        /// </summary>
        private class TargetDecoyGenerator
        {
            public bool[] EligibleScores { get; private set; }

            public IList<IPeakFeatureCalculator> FeatureCalculators { get; private set; }

            private readonly PeakTransitionGroupFeatures[] _peakTransitionGroupFeaturesList;

            public Dictionary<KeyValuePair<int, int>, PeakTransitionGroupFeatures> PeakTransitionGroupDictionary { get; private set; }
            public TargetDecoyGenerator(IPeakScoringModel scoringModel)
            {
                // Determine which calculators will be used to score peaks in this document.
                var document = Program.ActiveDocumentUI;
                FeatureCalculators = scoringModel.PeakFeatureCalculators.ToArray();
                _peakTransitionGroupFeaturesList = document.GetPeakFeatures(FeatureCalculators).ToArray();
                PopulateDictionary();

                EligibleScores = new bool[FeatureCalculators.Count];
                // Disable calculators that have only a single score value or any unknown scores.
                for (int i = 0; i < FeatureCalculators.Count; i++)
                    EligibleScores[i] = IsValidCalculator(i);
            }

            private void PopulateDictionary()
            {
                PeakTransitionGroupDictionary = new Dictionary<KeyValuePair<int, int>, PeakTransitionGroupFeatures>();
                foreach (var transitionGroupFeatures in _peakTransitionGroupFeaturesList)
                {
                    var pepId = transitionGroupFeatures.Id.NodePep.Id.GlobalIndex;
                    var fileId = transitionGroupFeatures.Id.ChromatogramSet.FindFile(transitionGroupFeatures.Id.FilePath).GlobalIndex;
                    PeakTransitionGroupDictionary.Add(new KeyValuePair<int, int>(pepId, fileId), transitionGroupFeatures);
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

                    if (!transitionGroup.Any())
                        continue;
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
            /// <param name="scoringParams">Parameters to choose the best peak</param>
            /// <param name="calculatorParams">Parameters to calculate the score of the best peak.</param>
            /// <param name="targetScores">Output list of target scores.</param>
            /// <param name="decoyScores">Output list of decoy scores.</param>
            /// <param name="secondBestScores">Output list of false target scores.</param>
            /// <param name="invert">If true, select minimum rather than maximum scores</param>
            public void GetScores(LinearModelParams scoringParams, LinearModelParams calculatorParams, out List<double> targetScores, out List<double> decoyScores,
                                  out List<double> secondBestScores, bool invert = false)
            {
                targetScores = new List<double>();
                decoyScores = new List<double>();
                secondBestScores = new List<double>();
                int invertSign = invert ? -1 : 1;

                foreach (var peakTransitionGroupFeatures in _peakTransitionGroupFeaturesList)
                {
                    PeakGroupFeatures maxFeatures = null;
                    PeakGroupFeatures nextFeatures = null;
                    double maxScore = double.MinValue;
                    double nextScore = double.MinValue;

                    // No peaks in this transition group record
                    if (peakTransitionGroupFeatures.PeakGroupFeatures.Count == 0)
                        continue;

                    // Find the highest and second highest scores among the transitions in this group.
                    foreach (var peakGroupFeatures in peakTransitionGroupFeatures.PeakGroupFeatures)
                    {
                        double score = invertSign * GetScore(scoringParams, peakGroupFeatures);
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

                    double currentScore = maxFeatures == null ? double.NaN : GetScore(calculatorParams, maxFeatures);
                    if (peakTransitionGroupFeatures.Id.NodePep.IsDecoy)
                        decoyScores.Add(currentScore);
                    else
                    {
                        targetScores.Add(currentScore);
                        // Skip if only one peak
                        if (peakTransitionGroupFeatures.PeakGroupFeatures.Count == 1)
                            continue;
                        double secondBestScore = nextFeatures == null ? double.NaN : GetScore(calculatorParams, nextFeatures);
                        secondBestScores.Add(secondBestScore);

                    }
                }
            }

            /// <summary>
            ///  Is the specified calculator valid for this dataset (has no unknown values and not all the same value)?
            /// </summary>
            private bool IsValidCalculator(int calculatorIndex)
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
                            return false;
                        maxValue = Math.Max(value, maxValue);
                        minValue = Math.Min(value, minValue);
                    }
                }
                return maxValue > minValue;
            }

            /// <summary>
            /// Calculate the score of a set of features given an array of weighting coefficients.
            /// </summary>
            private static double GetScore(IList<double> weights, PeakGroupFeatures peakGroupFeatures, double bias)
            {
                // TODO: Can we avoid this allocation?  Why are features floats sometimes and doubles at other times?
                return LinearModelParams.Score(ToDoubles(peakGroupFeatures.Features), weights, bias);
            }

            private static double GetScore(LinearModelParams parameters, PeakGroupFeatures peakGroupFeatures)
            {
                return GetScore(parameters.Weights, peakGroupFeatures, parameters.Bias);
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
                    Items.Add(new PeakCalculatorWeight(calculators[i].Name, null, null, true));
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
        public double? PercentContribution { get; set; }
        public bool IsEnabled { get; set; }

        public PeakCalculatorWeight(string name, double? weight, double? percentContribution, bool enabled)
        {
            Name = name;
            Weight = weight;
            PercentContribution = percentContribution;
            IsEnabled = enabled;
        }
    }
}
