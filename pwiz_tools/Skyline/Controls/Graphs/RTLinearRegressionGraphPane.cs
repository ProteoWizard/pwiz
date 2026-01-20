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
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Common.SystemUtil.Caching;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.RetentionTimes;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using ZedGraph;

namespace pwiz.Skyline.Controls.Graphs
{
    public sealed class RTLinearRegressionGraphPane : SummaryGraphPane, IUpdateGraphPaneController, IDisposable, ITipDisplayer
    {
        public static ReplicateDisplay ShowReplicate
        {
            get
            {
                return Helpers.ParseEnum(Settings.Default.ShowRegressionReplicateEnum, ReplicateDisplay.all);
            }
        }

        public static readonly Color COLOR_REFINED = Color.DarkBlue;
        public static readonly Color COLOR_LINE_REFINED = Color.Black;
        public static readonly Color COLOR_LINE_PREDICT = Color.DarkGray;
        public static readonly Color COLOR_OUTLIERS = Color.BlueViolet;
        public static readonly Color COLOR_LINE_ALL = Color.BlueViolet;

        private GraphData _data;
        private NodeTip _tip;
        private int _progressValue = -1;
        private Stopwatch _progressStopwatch;
        private const int PROGRESS_INITIAL_DELAY_MS = 300; // Wait before showing progress bar
        private const int PROGRESS_UPDATE_INTERVAL_MS = 100; // Throttle progress UI updates after first show
        public PaneProgressBar _progressBar;
        private ReplicateCachingReceiver<RetentionTimeRegressionSettings, RtRegressionResults> _graphDataReceiver;

        public RTLinearRegressionGraphPane(GraphSummary graphSummary, bool runToRun)
            : base(graphSummary)
        {
            XAxis.Title.Text = GraphsResources.RTLinearRegressionGraphPane_RTLinearRegressionGraphPane_Score;
            RunToRun = runToRun;
            Settings.Default.RTScoreCalculatorList.ListChanged += RTScoreCalculatorList_ListChanged;
            AllowDisplayTip = true;
            var receiver = _producer.RegisterCustomer(GraphSummary, ProductAvailableAction);
            _graphDataReceiver = new ReplicateCachingReceiver<RetentionTimeRegressionSettings, RtRegressionResults>(
                receiver,
                ReplicateCachingReceiver<RetentionTimeRegressionSettings, RtRegressionResults>.DefaultCleanCache);
            _graphDataReceiver.ProgressChange += ProgressChangeAction;
        }

        public void Dispose()
        {
            _graphDataReceiver?.Dispose();
            _progressBar?.Dispose();
            _progressBar = null;
            AllowDisplayTip = false;
            Settings.Default.RTScoreCalculatorList.ListChanged -= RTScoreCalculatorList_ListChanged;
        }

        public override bool HasToolbar { get { return RunToRun; } }

        public bool UpdateUIOnIndexChanged()
        {
            return true;
        }

        public bool UpdateUIOnLibraryChanged()
        {
            return ShowReplicate == ReplicateDisplay.single && !RunToRun;
        }

        private void RTScoreCalculatorList_ListChanged(object sender, EventArgs e)
        {
            if (GraphSummary.IsHandleCreated)
            {
                GraphSummary.BeginInvoke(new Action(() =>
                {
                    // Clear cache because newly initialized calculators require fresh computation.
                    // Without this, the cached partial result (with GraphData == null) would be returned.
                    _graphDataReceiver.ClearCache();
                    UpdateGraph(false);
                }));
            }
        }

        private void ProductAvailableAction()
        {
            ProgressChangeAction();
            UpdateGraph(false);
        }

        private void ProgressChangeAction()
        {
            if (_graphDataReceiver.IsProcessing())
            {
                int newProgressValue = _graphDataReceiver.GetProgressValue();
                if (newProgressValue != _progressValue)
                {
                    if (_progressStopwatch == null)
                    {
                        // First progress update - start timing but don't show yet
                        _progressStopwatch = Stopwatch.StartNew();
                        _progressValue = newProgressValue;
                        return;
                    }

                    bool progressBarShowing = _progressBar != null;
                    int throttleMs = progressBarShowing ? PROGRESS_UPDATE_INTERVAL_MS : PROGRESS_INITIAL_DELAY_MS;

                    if (_progressStopwatch.ElapsedMilliseconds < throttleMs)
                    {
                        return; // Skip this update, not enough time has passed
                    }

                    _progressStopwatch.Restart();
                    _progressBar ??= new PaneProgressBar(this);
                    _progressBar.UpdateProgress(newProgressValue);
                    _progressValue = newProgressValue;
                }
            }
            else
            {
                _progressBar?.Dispose();
                _progressBar = null;
                _progressValue = -1;
                _progressStopwatch = null;
            }
        }

        public override bool HandleMouseMoveEvent(ZedGraphControl sender, MouseEventArgs e)
        {
            var peptideIndex = PeptideIndexFromPoint(new PointF(e.X, e.Y));
            if (peptideIndex != null)
            {
                double? x = peptideIndex.X;
                double? y = peptideIndex.Y;
                if (RTGraphController.PlotType == PlotTypeRT.residuals && Data != null &&
                    Data.ResidualsRegression != null && Data.ResidualsRegression.Conversion != null)
                    y = Data.GetYResidual(peptideIndex);

                if (_tip == null)
                    _tip = new NodeTip(this);

                _tip.SetTipProvider(
                    new PeptideRegressionTipProvider(peptideIndex.ModifiedTarget, XAxis.Title.Text, YAxis.Title.Text,
                        x, y),
                    new Rectangle(e.Location, new Size()),
                    e.Location);

                sender.Cursor = Cursors.Hand;
                return true;
            }
            else
            {
                if (_tip != null)
                    _tip.HideTip();

                return base.HandleMouseMoveEvent(sender, e);
            }
        }

        public override bool HandleMouseDownEvent(ZedGraphControl sender, MouseEventArgs e)
        {
            var peptideIndex = PeptideIndexFromPoint(new PointF(e.X, e.Y));
            if (peptideIndex != null)
            {
                var pathSelect = peptideIndex.IdentityPath;
                SelectPeptide(pathSelect);
                return true;
            }
            return false;
        }

        private bool IsResiduals(GraphData graphData)
        {
            return RTGraphController.PlotType == PlotTypeRT.residuals &&
                   null != graphData?.ResidualsRegression?.Conversion;
        }
        public bool RunToRun { get; private set; }

        public void SelectPeptide(IdentityPath peptidePath)
        {
            GraphSummary.StateProvider.SelectedPath = peptidePath;
            if (ShowReplicate == ReplicateDisplay.best && !RunToRun)
            {
                var document = GraphSummary.DocumentUIContainer.DocumentUI;
                var nodePep = (PeptideDocNode)document.FindNode(peptidePath);
                int resultsIndex = nodePep.BestResult;
                if (resultsIndex != -1)
                    GraphSummary.StateProvider.SelectedResultsIndex = resultsIndex;
            }
        }

        public bool AllowDeletePoint(PointF point)
        {
            return PeptideIndexFromPoint(point) != null;
        }

        private GraphData Data
        {
            get
            {
                return _data;
            }
            set
            {
                _data = value;
            }
        }

        public bool HasOutliers
        {
            get
            {
                var data = Data;
                return data != null && data.HasOutliers;
            }
        }

        public PeptideDocNode[] Outliers
        {
            get
            {
                return Data?.Outliers.Select(pt => (PeptideDocNode)Data.Document.FindNode(pt.IdentityPath)).ToArray();
            }
        }

        public RetentionTimeRegression RegressionRefined
        {
            get
            { 
                GraphData data = Data;
                return data == null ? null : data.RegressionRefined;
            }
        }

        public RetentionTimeStatistics StatisticsRefined
        {
            get
            {
                GraphData data = Data;
                return data == null ? null : data.StatisticsRefined;
            }
        }

        public void Clear()
        {
            Data = null;
            Title.Text = string.Empty;
            CurveList.Clear();
            GraphObjList.Clear();
        }

        public void Graph(IdentityPath selectedPeptide)
        {
            if (RTGraphController.PlotType == PlotTypeRT.correlation)
            {
                GraphCorrelation(selectedPeptide);
            }
            else
            {
                GraphResiduals(selectedPeptide);
            }
        }

        private static bool IsDataRefined(GraphData data)
        {
            return data != null && data.IsRefined();
        }

        public bool IsRefined
        {
            get { return IsDataRefined(Data); }
        }

        public bool RegressionRefinedNull => Data.RegressionRefinedNull;

        public override void Draw(Graphics g)
        {
            var data = Data;
            if (data != null && RTGraphController.PlotType == PlotTypeRT.correlation)
            {
                // Force Axes to recalculate to ensure proper layout of labels
                AxisChange(g);
                data.AddLabels(this, g);
            }

            base.Draw(g);
        }

        private PointInfo PeptideIndexFromPoint(PointF point)
        {
            var data = Data;
            if (data == null)
            {
                return null;
            }

            bool residuals = IsResiduals(data);
            foreach (var pointInfo in data.AllPoints)
            {
                double y = residuals ? Data.GetYResidual(pointInfo) : Data.GetYCorrelation(pointInfo);
                double x = Data.GetX(pointInfo);
                if (PointIsOver(point, x, y))
                {
                    return pointInfo;
                }
            }

            return null;
        }

        private const int OVER_THRESHOLD = 4;

        public bool PointIsOver(PointF point, double score, double time)
        {
            float x = XAxis.Scale.Transform(score);
            if (Math.Abs(x - point.X) > OVER_THRESHOLD)
                return false;
            float y = YAxis.Scale.Transform(time);
            if (Math.Abs(y - point.Y) > OVER_THRESHOLD)
                return false;
            return true;
        }

        private bool _inUpdate;
        public override void UpdateGraph(bool selectionChanged)
        {
            if (_inUpdate)
            {
                return;
            }
            try
            {
                _inUpdate = true;
                UpdateNow();
            }
            finally
            {
                _inUpdate = false;
            }
        }

        private void UpdateNow()
        {
            var regressionSettings = GetRegressionSettings();
            RtRegressionResults results;
            try
            {
                if (!_graphDataReceiver.TryGetProduct(regressionSettings, out results))
                {
                    // Keep showing previous graph while calculating new data (stale-while-revalidate)
                    return;
                }
            }
            catch (Exception e)
            {
                ExceptionUtil.DisplayOrReportException(Program.MainWindow, e);
                return;
            }

            if (UpdateInitializedCalculators(results.InitializedCalculators))
            {
                return;
            }

            if (results.GraphData == null)
            {
                return;
            }

            // Clear only when new data is ready for seamless transition
            GraphObjList.Clear();
            CurveList.Clear();
            _data = results.GraphData;
            GraphHelper.FormatGraphPane(this);

            var nodeTree = GraphSummary.StateProvider.SelectedNode as SrmTreeNode;
            var nodePeptide = nodeTree as PeptideTreeNode;
            while (nodePeptide == null && nodeTree != null)
            {
                nodeTree = nodeTree.Parent as SrmTreeNode;
                nodePeptide = nodeTree as PeptideTreeNode;
            }

            IdentityPath selectedPeptidePath = null;
            if (nodePeptide != null)
            {
                selectedPeptidePath = nodeTree.Path;
            }

            Legend.IsVisible = true;
            Title.Text = null;
            Graph(selectedPeptidePath);
            
            if (Data.TargetIndex < 0)
            {
                if (RTGraphController.PlotType == PlotTypeRT.correlation)
                {
                    YAxis.Title.Text = Resources.RTLinearRegressionGraphPane_RTLinearRegressionGraphPane_Measured_Time;
                }
                else
                {
                    YAxis.Title.Text = Data._regressionPredict != null
                        ? GraphsResources.GraphData_GraphResiduals_Time_from_Prediction
                        : Resources.GraphData_GraphResiduals_Time_from_Regression;
                }
            }
            else
            {
                var chromatogramSetYAxis =
                    Data.Document.MeasuredResults?.Chromatograms.ElementAtOrDefault(Data.TargetIndex);
                if (RTGraphController.PlotType == PlotTypeRT.correlation)
                {
                    YAxis.Title.Text = string.Format(GraphsResources.GraphData_CorrelationLabel_Measured_Time___0__,
                        chromatogramSetYAxis?.Name);
                }
                else
                {
                    YAxis.Title.Text = string.Format(
                        GraphsResources.GraphData_ResidualsLabel_Time_from_Regression___0__,
                        chromatogramSetYAxis?.Name);
                }
            }
            if (Data.IsRunToRun)
            {
                var chromatogramSetXAxis =
                    Data.Document.MeasuredResults?.Chromatograms.ElementAtOrDefault(Data.OriginalIndex);
                if (chromatogramSetXAxis != null)
                {
                    XAxis.Title.Text = string.Format(GraphsResources.GraphData_CorrelationLabel_Measured_Time___0__,
                        chromatogramSetXAxis.Name);
                }
                else
                {
                    XAxis.Title.Text = string.Empty;
                }
            }
            else
            {
                if (Data.RegressionSettings.CalculatorName != null)
                {
                    XAxis.Title.Text = Data.RegressionSettings.CalculatorName.AxisTitle;
                }
                else if (Data.Calculator != null)
                {
                    XAxis.Title.Text = new AlignmentTarget.Irt(Data.Calculator).GetAxisTitle(RTPeptideValue.Retention);
                }
                else
                {
                    XAxis.Title.Text = string.Empty;
                }
            }
            AxisChange();
            GraphSummary.GraphControl.Invalidate();
        }

        private bool _allowDisplayTip;

        private bool UpdateInitializedCalculators(IEnumerable<InitializedRetentionScoreCalculator> calcs)
        {
            bool anyChanges = false;
            var settingsList = Settings.Default.RTScoreCalculatorList;
            foreach (var calc in calcs)
            {
                if (calc.Initialized != null && settingsList.Contains(calc.Original))
                {
                    settingsList.SetValue(calc.Initialized);
                    anyChanges = true;
                }
            }

            return anyChanges;
        }
        public bool IsCalculating
        {
            get
            {
                return _graphDataReceiver.IsProcessing();
            }
        }

        /// <summary>
        /// Returns true when the graph data is fully loaded and ready to be accessed.
        /// Use this in tests instead of just IsCalculating to avoid race conditions
        /// where the background calculation is done but the data hasn't been set yet.
        /// </summary>
        public bool IsComplete
        {
            get
            {
                // Error means we're "done" (no further processing will help)
                if (_graphDataReceiver.HasError)
                    return true;

                // Not complete if still calculating
                if (IsCalculating)
                    return false;

                // Data must be populated (ProductAvailableAction callback has run)
                return StatisticsRefined != null;
            }
        }

        private RetentionTimeRegressionSettings GetRegressionSettings()
        {
            var targetIndex = ShowReplicate == ReplicateDisplay.single || RunToRun
                ? GraphSummary.TargetResultsIndex
                : -1;
            var originalIndex = RunToRun ? GraphSummary.OriginalResultsIndex : -1;
            var pointsType = RTGraphController.PointsType;
            var document = GraphSummary.DocumentUIContainer.DocumentUI;
            if (pointsType == PointsTypeRT.standards && !document.GetRetentionTimeStandards().Any())
            {
                pointsType = PointsTypeRT.targets;
            }
            if (pointsType == PointsTypeRT.decoys && !document.Molecules.Any(pep=>pep.IsDecoy))
            {
                pointsType = PointsTypeRT.targets;
            }
            if (RTGraphController.PointsType == PointsTypeRT.targets_fdr && targetIndex == -1)
            {
                pointsType = PointsTypeRT.targets;
            }

            return new RetentionTimeRegressionSettings(document, targetIndex, originalIndex, ShowReplicate == ReplicateDisplay.best,
                RTGraphController.OutThreshold,
                Settings.Default.RTRefinePeptides && RTGraphController.CanDoRefinementForRegressionMethod, pointsType,
                RTGraphController.RegressionMethod, Settings.Default.RtCalculatorOption, RunToRun);
        }

        /// <summary>
        /// Holds the data currently displayed in the graph.
        /// </summary>
        sealed class GraphData : Immutable
        {
            private readonly ImmutableList<PointInfo> _points;
            private ImmutableList<PointInfo> _outlierPoints;
            private ImmutableList<PointInfo> _refinedPoints;

            public readonly RetentionTimeRegression _regressionPredict;
            public readonly IRegressionFunction _conversionPredict;
            public readonly RetentionTimeStatistics _statisticsPredict;

            public readonly RetentionTimeRegression _regressionAll;
            public readonly RetentionTimeStatistics _statisticsAll;

            public RetentionTimeRegression _regressionRefined;
            public RetentionTimeStatistics _statisticsRefined;

            private readonly RetentionScoreCalculatorSpec _calculator;

            public RetentionScoreCalculatorSpec Calculator { get { return _calculator; } }

            public GraphData(RetentionTimeRegressionSettings regressionSettings, ProductionMonitor productionMonitor)
            {
                RegressionSettings = regressionSettings;
                var snapshot = RetentionTimeRegressionGraphData.ComputeSnapshot(regressionSettings,
                    productionMonitor);

                _regressionPredict = snapshot.RegressionPredict;
                _conversionPredict = snapshot.ConversionPredict;
                _statisticsPredict = snapshot.StatisticsPredict;
                _regressionAll = snapshot.RegressionAll;
                _statisticsAll = snapshot.StatisticsAll;
                _regressionRefined = snapshot.RegressionRefined;
                _statisticsRefined = snapshot.StatisticsRefined;
                _calculator = snapshot.Calculator;

                _points = snapshot.AllPoints.Select(dp => new PointInfo(dp.IdentityPath, dp.ModifiedTarget, dp.X, dp.Y)).ToImmutable();
                _refinedPoints = snapshot.RefinedPoints.Select(dp => new PointInfo(dp.IdentityPath, dp.ModifiedTarget, dp.X, dp.Y)).ToImmutable();
                _outlierPoints = snapshot.Outliers.Select(dp => new PointInfo(dp.IdentityPath, dp.ModifiedTarget, dp.X, dp.Y)).ToImmutable();
            }

            public SrmDocument Document
            {
                get { return RegressionSettings.Document; }
            }
            public RetentionTimeRegressionSettings RegressionSettings { get; private set; }

            public bool IsRunToRun
            {
                get { return RegressionSettings.IsRunToRun; }
            }

            public int TargetIndex { get { return RegressionSettings.TargetIndex; } }

            public int OriginalIndex { get { return RegressionSettings.OriginalIndex; } }

            public RetentionTimeRegression RegressionRefined
            {
                get { return _regressionRefined ?? _regressionAll; }
            }

            public RetentionTimeStatistics StatisticsRefined
            {
                get { return _statisticsRefined ?? _statisticsAll; }
            }

            public bool RegressionRefinedNull => _regressionRefined == null;

            public bool IsRefined()
            {
                // If refinement has been performed, or it doesn't need to be.
                if (_regressionRefined != null)
                    return true;
                if (_statisticsAll == null)
                    return false;
                return RetentionTimeRegression.IsAboveThreshold(_statisticsAll.R, RegressionSettings.Threshold);
            }

            public bool HasOutliers { get { return 0 < _outlierPoints.Count; } }

            /// <summary>
            /// Returns the appropriate label for points based on whether refinement was requested.
            /// Shows "Peptides Refined" (or molecule equivalent) when refinement is enabled,
            /// regardless of whether outliers were actually found.
            /// </summary>
            public string GetPointsLabel()
            {
                return Helpers.PeptideToMoleculeTextMapper.Translate(
                    RegressionSettings.Refine ? GraphsResources.GraphData_Graph_Peptides_Refined : GraphsResources.GraphData_Graph_Peptides,
                    Document.DocumentType);
            }

            /// <summary>
            /// Returns the appropriate label for the regression line based on whether refinement was requested.
            /// Shows "Regression Refined" when refinement is enabled, regardless of whether outliers were found.
            /// </summary>
            public string GetRegressionLabel()
            {
                return RegressionSettings.Refine ? GraphsResources.GraphData_Graph_Regression_Refined : GraphsResources.GraphData_Graph_Regression;
            }

            public ImmutableList<PointInfo> AllPoints
            {
                get { return _points; }
            }
            public ImmutableList<PointInfo> RefinedPoints
            {
                get { return _refinedPoints; }
            }
            public ImmutableList<PointInfo> Outliers
            {
                get { return _outlierPoints; }
            }

            public RetentionTimeRegression ResidualsRegression
            {
                get
                {
                    var regressions = new[]
                    {
                        _regressionPredict, _regressionRefined, _regressionAll
                    };
                    return regressions.Where(regression => null != regression?.Conversion).Concat(regressions)
                        .FirstOrDefault(regression => null != regression);
                }
            }

            private IRegressionFunction GetConversion(RetentionTimeRegression regression)
            {
                if (regression == null)
                    return null;
                if (ReferenceEquals(regression, _regressionPredict) && _conversionPredict != null)
                    return _conversionPredict;
                return regression.Conversion;
            }


            public void AddLabels(GraphPane graphPane, Graphics g)
            {
                RectangleF rectChart = graphPane.Chart.Rect;
                PointF ptTop = rectChart.Location;

                // Setup axes scales to enable the ReverseTransform method
                var xAxis = graphPane.XAxis;
                xAxis.Scale.SetupScaleData(graphPane, xAxis);
                var yAxis = graphPane.YAxis;
                yAxis.Scale.SetupScaleData(graphPane, yAxis);

                float yNext = ptTop.Y;
                double scoreLeft = xAxis.Scale.ReverseTransform(ptTop.X + 8);
                double timeTop = yAxis.Scale.ReverseTransform(yNext);

                graphPane.GraphObjList.RemoveAll(o => o is TextObj);

                if (!HasOutliers)
                {
                    yNext += AddRegressionLabel(graphPane, g, scoreLeft, timeTop,
                        _regressionAll, _statisticsAll, COLOR_LINE_REFINED);
                }
                else
                {
                    yNext += AddRegressionLabel(graphPane, g, scoreLeft, timeTop,
                        _regressionRefined, _statisticsRefined, COLOR_LINE_REFINED);
                    timeTop = yAxis.Scale.ReverseTransform(yNext);
                    yNext += AddRegressionLabel(graphPane, g, scoreLeft, timeTop,
                        _regressionAll, _statisticsAll, COLOR_LINE_ALL);
                }

                if (_regressionPredict != null &&
                    _regressionPredict.Conversion != null &&
                    Settings.Default.RTPredictorVisible)
                {
                    timeTop = yAxis.Scale.ReverseTransform(yNext);
                    AddRegressionLabel(graphPane, g, scoreLeft, timeTop,
                                       _regressionPredict, _statisticsPredict, COLOR_LINE_PREDICT);
                }
            }

            private float AddRegressionLabel(PaneBase graphPane, Graphics g, double score, double time,
                                                    RetentionTimeRegression regression, RetentionTimeStatistics statistics, Color color)
            {
                string label;
                var conversion = GetConversion(regression);
                if (conversion == null || statistics == null)
                {
                    // ReSharper disable LocalizableElement
                    label = String.Format("{0} = ?, {1} = ?\n" + "{2} = ?\n" + "r = ?",
                                          Resources.Regression_slope,
                                          Resources.Regression_intercept,
                                          Resources.GraphData_AddRegressionLabel_window);
                    // ReSharper restore LocalizableElement
                }
                else
                {
                    label = regression.Conversion.GetRegressionDescription(statistics.R, regression.TimeWindow);
                }


                TextObj text = new TextObj(label, score, time,
                                           CoordType.AxisXYScale, AlignH.Left, AlignV.Top)
                                   {
                                       IsClippedToChartRect = true,
                                       ZOrder = ZOrder.E_BehindCurves,
                                       FontSpec = GraphSummary.CreateFontSpec(color),
                                   };
                graphPane.GraphObjList.Add(text);

                // Measure the text just added, and return its height
                SizeF sizeLabel = text.FontSpec.MeasureString(g, label, graphPane.CalcScaleFactor());
                return sizeLabel.Height + 3;
            }

            public double GetX(PointInfo point)
            {
                return point.X ?? Calculator?.UnknownScore ?? 0;
            }

            public double GetY(PlotTypeRT plotType, PointInfo point)
            {
                return plotType == PlotTypeRT.correlation ? GetYCorrelation(point) : GetYResidual(point);
            }

            public double GetYCorrelation(PointInfo pt)
            {
                return pt.Y ?? Calculator?.UnknownScore ?? 0;
            }

            public double GetYResidual(PointInfo pt)
            {
                if (!pt.X.HasValue || !pt.Y.HasValue)
                {
                    return PointPairBase.Missing;
                }

                var residualsRegression = ResidualsRegression;
                if (residualsRegression == null)
                {
                    return PointPairBase.Missing;
                }

                IRegressionFunction conversion = null;
                if (ReferenceEquals(residualsRegression, _regressionPredict))
                {
                    conversion = _conversionPredict;
                }

                conversion ??= residualsRegression.Conversion;
                var expected = conversion.GetY(pt.X.Value);
                var residual = pt.Y.Value - expected;
                return Math.Round(residual, 6);
            }
        }

        private void GraphRegression(RetentionTimeStatistics statistics, RetentionTimeRegression regression, string name, Color color)
        {
            double[] lineScores, lineTimes;
            if (statistics == null || regression == null)
            {
                lineScores = new double[0];
                lineTimes = new double[0];
            }
            else
            {
                regression.Conversion.GetCurve(statistics, out lineScores, out lineTimes);
            }
            var curve = AddCurve(name, lineScores, lineTimes, color, SymbolType.None);
            if (lineScores.Length > 0 && lineTimes.Length > 0)
            {
                AddCurve(string.Empty, new[] { lineScores[0] }, new[] { lineTimes[0] }, color, SymbolType.Square);
                AddCurve(string.Empty, new[] { lineScores.Last() }, new[] { lineTimes.Last() }, color, SymbolType.Square);
            }

            curve.Line.IsAntiAlias = true;
            curve.Line.IsOptimizedDraw = true;
        }


        private PointInfo PointFromPeptide(IdentityPath selectedPeptide)
        {
            if (selectedPeptide == null)
            {
                return null;
            }

            return Data?.AllPoints.FirstOrDefault(pt => selectedPeptide.Equals(pt.IdentityPath));
        }

        private void GraphCorrelation(IdentityPath selectedPeptide)
        {
            if (YAxis.Scale.MinAuto)
            {
                YAxis.Scale.MinAuto = false;
                YAxis.Scale.Min = 0;
            }

            PointInfo selectedPoint = PointFromPeptide(selectedPeptide);

            if (selectedPoint != null)
            {
                Color colorSelected = GraphSummary.ColorSelected;
                var curveOut = AddCurve(null, new[] { Data.GetX(selectedPoint) }, new[] { Data.GetYCorrelation(selectedPoint) },
                    colorSelected, SymbolType.Diamond);
                curveOut.Line.IsVisible = false;
                curveOut.Symbol.Fill = new Fill(colorSelected);
                curveOut.Symbol.Size = 8f;
            }

            string labelPoints = Data.GetPointsLabel();
            if (Data.HasOutliers)
            {
                // Refinement with outliers - show both refined and unrefined lines
                GraphRegression(Data._statisticsRefined, Data._regressionAll, GraphsResources.GraphData_Graph_Regression_Refined, COLOR_LINE_REFINED);
                GraphRegression(Data._statisticsAll, Data._regressionAll, GraphsResources.GraphData_Graph_Regression, COLOR_LINE_ALL);
            }
            else
            {
                // No outliers - show single line with label based on refinement setting
                GraphRegression(Data._statisticsAll, Data._regressionAll, Data.GetRegressionLabel(), COLOR_LINE_REFINED);
            }

            if (Data._regressionPredict != null && Settings.Default.RTPredictorVisible)
            {
                GraphRegression(Data._statisticsPredict, Data._regressionAll, GraphsResources.GraphData_Graph_Predictor, COLOR_LINE_PREDICT);
            }

            var curve = AddCurve(labelPoints, Data.RefinedPoints.Select(Data.GetX).ToArray(),
                Data.RefinedPoints.Select(Data.GetYCorrelation).ToArray(), Color.Black, SymbolType.Diamond);
            curve.Line.IsVisible = false;
            curve.Symbol.Border.IsVisible = false;
            curve.Symbol.Fill = new Fill(COLOR_REFINED);

            if (Data.HasOutliers)
            {
                var curveOut = AddCurve(GraphsResources.GraphData_Graph_Outliers, Data.Outliers.Select(Data.GetX).ToArray(),
                    Data.Outliers.Select(Data.GetYCorrelation).ToArray(), Color.Black, SymbolType.Diamond);
                curveOut.Line.IsVisible = false;
                curveOut.Symbol.Border.IsVisible = false;
                curveOut.Symbol.Fill = new Fill(COLOR_OUTLIERS);
            }
        }

        private void GraphResiduals(IdentityPath selectedPeptide)
        {
            if (!YAxis.Scale.MinAuto && ZoomStack.Count == 0)
            {
                YAxis.Scale.MinAuto = true;
                YAxis.Scale.MaxAuto = true;
            }

            var regression = Data.ResidualsRegression;
            if (regression == null || regression.Conversion == null)
                return;

            var ptSelected = PointFromPeptide(selectedPeptide);
            if (null != ptSelected)
            {
                Color colorSelected = GraphSummary.ColorSelected;
                var curveOut = AddCurve(null, new[] { Data.GetX(ptSelected) }, new[] { Data.GetYResidual(ptSelected) },
                    colorSelected, SymbolType.Diamond);
                curveOut.Line.IsVisible = false;
                curveOut.Symbol.Fill = new Fill(colorSelected);
                curveOut.Symbol.Size = 8f;
            }

            string labelPoints = Data.GetPointsLabel();
            var curve = AddCurve(labelPoints, Data.RefinedPoints.Select(Data.GetX).ToArray(),
                Data.RefinedPoints.Select(Data.GetYResidual).ToArray(), Color.Black, SymbolType.Diamond);
            curve.Line.IsVisible = false;
            curve.Symbol.Border.IsVisible = false;
            curve.Symbol.Fill = new Fill(COLOR_REFINED);

            if (Data.HasOutliers)
            {
                var curveOut = AddCurve(GraphsResources.GraphData_Graph_Outliers, Data.Outliers.Select(Data.GetX).ToArray(),
                    Data.Outliers.Select(Data.GetYResidual).ToArray(), Color.Black, SymbolType.Diamond);
                curveOut.Line.IsVisible = false;
                curveOut.Symbol.Border.IsVisible = false;
                curveOut.Symbol.Fill = new Fill(COLOR_OUTLIERS);
            }
        }

        public Rectangle ScreenRect { get { return Screen.GetBounds(GraphSummary); } }

        public bool AllowDisplayTip
        {
            get { return !GraphSummary.IsDisposed && _allowDisplayTip; }
            private set { _allowDisplayTip = value; }
        }

        public Rectangle RectToScreen(Rectangle r)
        {
            return GraphSummary.RectangleToScreen(r);
        }

        private class PointInfo : Immutable
        {
            public PointInfo(IdentityPath identityPath, Target modifiedTarget, double? x, double? y)
            {
                IdentityPath = identityPath;
                ModifiedTarget = modifiedTarget;
                X = x;
                Y = y;
            }
            public Target ModifiedTarget { get; }
            public IdentityPath IdentityPath { get; }
            public double? X { get; private set; }

            public PointInfo ChangeX(double? value)
            {
                return ChangeProp(ImClone(this), im => im.X = value);
            }

            public double? Y { get; private set; }
            public PointInfo ChangeY(double? value)
            {
                return ChangeProp(ImClone(this), im => im.Y = value);
            }
        }

        private class RtRegressionResults : Immutable, ICachingResult
        {
            public RtRegressionResults(RetentionTimeRegressionSettings regressionSettings)
            {
                RegressionSettings = regressionSettings;
            }
            public RetentionTimeRegressionSettings RegressionSettings { get; private set; }
            public ImmutableList<InitializedRetentionScoreCalculator> InitializedCalculators { get; private set; }

            public RtRegressionResults ChangeInitializedCalculators(IEnumerable<InitializedRetentionScoreCalculator> calcs)
            {
                return ChangeProp(ImClone(this), im => im.InitializedCalculators = calcs.ToImmutable());
            }
            public GraphData GraphData { get; private set; }

            public RtRegressionResults ChangeGraphData(GraphData graphData)
            {
                return ChangeProp(ImClone(this), im => im.GraphData = graphData);
            }

            // ICachingResult implementation
            public SrmDocument Document => RegressionSettings.Document;
        }

        private static Producer<RetentionTimeRegressionSettings, RtRegressionResults> _producer = new DataProducer();
        private class DataProducer : Producer<RetentionTimeRegressionSettings, RtRegressionResults>
        {
            public override RtRegressionResults ProduceResult(ProductionMonitor productionMonitor, RetentionTimeRegressionSettings parameter, IDictionary<WorkOrder, object> inputs)
            {
                productionMonitor.SetProgress(0);
                var results = new RtRegressionResults(parameter)
                    .ChangeInitializedCalculators(inputs.Values.OfType<InitializedRetentionScoreCalculator>());
                if (results.InitializedCalculators.Any(calc => calc.Initialized != null))
                {
                    return results;
                }

                return results.ChangeGraphData(new GraphData(parameter, productionMonitor));
            }

            public override IEnumerable<WorkOrder> GetInputs(RetentionTimeRegressionSettings parameter)
            {
                var calculatorOption = parameter.CalculatorName;
                if (calculatorOption != null && !(calculatorOption is RtCalculatorOption.Irt))
                {
                    yield break;
                }
                foreach (var calculator in parameter.GetCalculators().Where(calc => !calc.IsUsable))
                {
                    yield return _rtScoreInitializer.MakeWorkOrder(calculator);
                }
            }
        }

        private static Producer<RetentionScoreCalculatorSpec, InitializedRetentionScoreCalculator> _rtScoreInitializer =
            new RtScoreInitializer();

        private class RtScoreInitializer : Producer<RetentionScoreCalculatorSpec, InitializedRetentionScoreCalculator>
        {
            public override InitializedRetentionScoreCalculator ProduceResult(ProductionMonitor productionMonitor,
                RetentionScoreCalculatorSpec parameter, IDictionary<WorkOrder, object> inputs)
            {
                if (parameter.IsUsable)
                {
                    return new InitializedRetentionScoreCalculator(parameter, parameter);
                }

                try
                {
                    return new InitializedRetentionScoreCalculator(parameter, parameter.Initialize(new SilentProgressMonitor(productionMonitor.CancellationToken)));
                }
                catch (Exception exception)
                {
                    return new InitializedRetentionScoreCalculator(parameter, exception);
                }
            }
        }

        private class InitializedRetentionScoreCalculator : Immutable
        {
            public InitializedRetentionScoreCalculator(RetentionScoreCalculatorSpec original,
                RetentionScoreCalculatorSpec initialized)
            {
                Original = original;
                Initialized = initialized;
            }

            public InitializedRetentionScoreCalculator(RetentionScoreCalculatorSpec original, Exception exception)
            {
                Original = original;
                Exception = exception;
            }

            public RetentionScoreCalculatorSpec Original { get; private set; }
            public RetentionScoreCalculatorSpec Initialized { get; private set; }

            public Exception Exception { get; private set; }
        }
    }
}
