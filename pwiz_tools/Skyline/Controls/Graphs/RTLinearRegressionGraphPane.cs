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
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Forms;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Common.SystemUtil.Caching;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;
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
        public PaneProgressBar _progressBar;
        private Receiver<RegressionSettings, RtRegressionResults> _graphDataReceiver;

        public RTLinearRegressionGraphPane(GraphSummary graphSummary, bool runToRun)
            : base(graphSummary)
        {
            XAxis.Title.Text = GraphsResources.RTLinearRegressionGraphPane_RTLinearRegressionGraphPane_Score;
            RunToRun = runToRun;
            Settings.Default.RTScoreCalculatorList.ListChanged += RTScoreCalculatorList_ListChanged;
            AllowDisplayTip = true;
            _graphDataReceiver = _producer.RegisterCustomer(GraphSummary, ProductAvailableAction);
            _graphDataReceiver.ProgressChange += ProgressChangeAction;
        }

        public void Dispose()
        {
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
                    _progressBar ??= new PaneProgressBar(this);
                    _progressBar?.UpdateProgress(_graphDataReceiver.GetProgressValue());
                    _progressValue = newProgressValue;
                }
            }
            else
            {
                _progressBar?.Dispose();
                _progressBar = null;
                _progressValue = -1;
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

                GraphSummary.Cursor = Cursors.Hand;
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

        public static PeptideDocNode[] CalcOutliers(SrmDocument document, double threshold, int? precision, bool bestResult)
        {
            var regressionSettings = new RegressionSettings(document, -1, -1, bestResult, threshold, false,
                RTGraphController.PointsType, RTGraphController.RegressionMethod, Settings.Default.RTCalculatorName,
                false);
            var productionMonitor = new ProductionMonitor(CancellationToken.None, _ => { });
            return new GraphData(regressionSettings, productionMonitor).Outliers
                .Select(pt => (PeptideDocNode) document.FindNode(pt.IdentityPath)).ToArray();
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
                double? y = residuals ? Data.GetYResidual(pointInfo) : pointInfo.Y;
                if (pointInfo.X.HasValue && y.HasValue)
                {
                    if (PointIsOver(point, pointInfo.X.Value, y.Value))
                    {
                        return pointInfo;
                    }
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
            GraphObjList.Clear();
            CurveList.Clear();
            var regressionSettings = GetRegressionSettings();
            if (!_graphDataReceiver.TryGetProduct(regressionSettings, out var results))
            {
                return;
            }

            if (UpdateInitializedCalculators(results.InitializedCalculators))
            {
                return;
            }

            _data = results.GraphData;
            if (_data == null)
            {
                return;
            }
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
            Graph(selectedPeptidePath);
            
            var chromatogramSetYAxis =
                Data.Document.MeasuredResults?.Chromatograms.ElementAtOrDefault(Data.TargetIndex);
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
            else
            {
                XAxis.Title.Text = Data.Calculator?.Name ?? string.Empty;
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

        private RegressionSettings GetRegressionSettings()
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

            return new RegressionSettings(document, targetIndex, originalIndex, ShowReplicate == ReplicateDisplay.best,
                RTGraphController.OutThreshold,
                Settings.Default.RTRefinePeptides && RTGraphController.CanDoRefinementForRegressionMethod, pointsType,
                RTGraphController.RegressionMethod, Settings.Default.RTCalculatorName, RunToRun);
        }

        private class RegressionSettings : Immutable
        {
            public RegressionSettings(SrmDocument document, int targetIndex, int originalIndex, bool bestResult,
                double threshold, bool refine, PointsTypeRT pointsType, RegressionMethodRT regressionMethod, string calculatorName, bool isRunToRun)
            {
                Document = document;
                TargetIndex = targetIndex;
                OriginalIndex = originalIndex;
                BestResult = bestResult;
                Threshold = threshold;
                Refine = refine;
                PointsType = pointsType;
                RegressionMethod = regressionMethod;
                CalculatorName = calculatorName;
                if (!string.IsNullOrEmpty(CalculatorName))
                    Calculators = ImmutableList.Singleton(Settings.Default.GetCalculatorByName(calculatorName));
                else
                    Calculators = Settings.Default.RTScoreCalculatorList.ToImmutable();
                IsRunToRun = isRunToRun;
            }

            protected bool Equals(RegressionSettings other)
            {
                return ReferenceEquals(Document, other.Document) && TargetIndex == other.TargetIndex &&
                       OriginalIndex == other.OriginalIndex && BestResult == other.BestResult &&
                       Threshold.Equals(other.Threshold) && Refine == other.Refine && PointsType == other.PointsType &&
                       RegressionMethod == other.RegressionMethod && CalculatorName == other.CalculatorName &&
                       Equals(Calculators, other.Calculators) && IsRunToRun == other.IsRunToRun;
            }

            public override bool Equals(object obj)
            {
                if (obj is null) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != GetType()) return false;
                return Equals((RegressionSettings)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = RuntimeHelpers.GetHashCode(Document);
                    hashCode = (hashCode * 397) ^ TargetIndex;
                    hashCode = (hashCode * 397) ^ OriginalIndex;
                    hashCode = (hashCode * 397) ^ BestResult.GetHashCode();
                    hashCode = (hashCode * 397) ^ Threshold.GetHashCode();
                    hashCode = (hashCode * 397) ^ Refine.GetHashCode();
                    hashCode = (hashCode * 397) ^ (int)PointsType;
                    hashCode = (hashCode * 397) ^ (int)RegressionMethod;
                    hashCode = (hashCode * 397) ^ (CalculatorName != null ? CalculatorName.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ (Calculators != null ? Calculators.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ IsRunToRun.GetHashCode();
                    return hashCode;
                }
            }

            public SrmDocument Document { get; private set; }
            public int TargetIndex { get; private set; }
            public int OriginalIndex { get; private set; }
            public bool BestResult { get; private set; }
            public double Threshold { get; private set; }
            public int? ThresholdPrecision { get; private set; }

            public RegressionSettings ChangeThresholdPrecision(int? value)
            {
                return ChangeProp(ImClone(this), im => im.ThresholdPrecision = value);
            }
            public bool Refine { get; private set; }
            public PointsTypeRT PointsType { get; private set; }
            public RegressionMethodRT RegressionMethod { get; private set; }
            public string CalculatorName { get; private set; }

            public RegressionSettings ChangeCalculatorName(string value)
            {
                return ChangeProp(ImClone(this), im => im.CalculatorName = value);
            }
            public ImmutableList<RetentionScoreCalculatorSpec> Calculators { get; private set; }

            public RegressionSettings ChangeCalculators(IEnumerable<RetentionScoreCalculatorSpec> value)
            {
                return ChangeProp(ImClone(this), im => im.Calculators = value.ToImmutable());
            }
            public bool IsRunToRun { get; private set; }
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
            private bool _refine;

            public RetentionScoreCalculatorSpec Calculator { get { return _calculator; } }

            public GraphData(RegressionSettings regressionSettings, ProductionMonitor productionMonitor)
            {
                RegressionSettings = regressionSettings;
                var document = Document;
                var token = productionMonitor.CancellationToken;
                bool refine = regressionSettings.Refine;
                var pointInfos = new List<PointInfo>();
                var standards = new HashSet<Target>();
                if (RTGraphController.PointsType == PointsTypeRT.standards)
                    standards = document.GetRetentionTimeStandards();
                
                // Only used if we are comparing two runs
                var modifiedTargets = IsRunToRun ? null : new HashSet<Target>();

                foreach (var peptideGroupDocNode in document.MoleculeGroups)
                foreach (var nodePeptide in peptideGroupDocNode.Molecules)
                {
                    if (false == modifiedTargets?.Add(nodePeptide.ModifiedTarget))
                    {
                        continue;
                    }
                    var identityPath = new IdentityPath(peptideGroupDocNode.PeptideGroup, nodePeptide.Peptide);
                    productionMonitor.CancellationToken.ThrowIfCancellationRequested();
                    switch (RTGraphController.PointsType)
                    {
                        case PointsTypeRT.targets:
                            if (nodePeptide.IsDecoy)
                                continue;
                            break;
                        case PointsTypeRT.targets_fdr:
                        {
                            if(nodePeptide.IsDecoy)
                                continue;

                            if (TargetIndex != -1 && GetMaxQValue(nodePeptide, TargetIndex) >= 0.01 ||
                                OriginalIndex != -1 && GetMaxQValue(nodePeptide, OriginalIndex) >= 0.01)
                                continue;
                            break;
                        }
                        case PointsTypeRT.standards:
                            if (!standards.Contains(document.Settings.GetModifiedSequence(nodePeptide))
                                    || nodePeptide.GlobalStandardType != StandardType.IRT)  // In case of 15N labeled peptides, the unlabeled form may also show up
                                continue;
                            break;
                        case PointsTypeRT.decoys:
                            if (!nodePeptide.IsDecoy)
                                continue;
                            break;
                    }

                    float? rtTarget = null;
                    
                    //Only used if we are doing run to run, otherwise we use scores
                    float? rtOrig = null;

                    if (RegressionSettings.OriginalIndex >= 0)
                        rtOrig = nodePeptide.GetSchedulingTime(RegressionSettings.OriginalIndex);

                    if (RegressionSettings.BestResult)
                    {
                        int iBest = nodePeptide.BestResult;
                        if (iBest != -1)
                            rtTarget = nodePeptide.GetSchedulingTime(iBest);
                    }
                    else
                        rtTarget = nodePeptide.GetSchedulingTime(RegressionSettings.TargetIndex);

                    pointInfos.Add(new PointInfo(identityPath, nodePeptide.ModifiedTarget, rtOrig, rtTarget));
                }
                var targetTimes = pointInfos.Where(pt => pt.Y.HasValue)
                    .Select(pt => new MeasuredRetentionTime(pt.ModifiedTarget, pt.Y.Value)).ToList();

                if (IsRunToRun)
                {
                    var targetTimesDict = pointInfos.Where(pt => pt.Y.HasValue)
                        .ToDictionary(pt => pt.ModifiedTarget, pt => pt.Y.Value);
                    var origTimesDict = pointInfos.Where(pt => pt.X.HasValue)
                        .ToDictionary(pt => pt.ModifiedTarget, pt => pt.X.Value);
                    _calculator = new DictionaryRetentionScoreCalculator(XmlNamedElement.NAME_INTERNAL, DocumentRetentionTimes.ConvertToMeasuredRetentionTimes(origTimesDict));
                    var alignedRetentionTimes = AlignedRetentionTimes.AlignLibraryRetentionTimes(targetTimesDict, origTimesDict, refine ? RegressionSettings.Threshold : 0, RegressionSettings.RegressionMethod, token);
                    if (alignedRetentionTimes != null)
                    {
                        _regressionAll = alignedRetentionTimes.Regression;
                        _statisticsAll = alignedRetentionTimes.RegressionStatistics;
                    }
                }
                else
                {
                    var usableCalculators = RegressionSettings.Calculators.Where(calc => calc.IsUsable).ToList();
                    if (RegressionSettings.CalculatorName == null)
                    {
                        var summary = RetentionTimeRegression.CalcBestRegressionBackground(XmlNamedElement.NAME_INTERNAL, usableCalculators, targetTimes, null, true,
                            RegressionSettings.RegressionMethod, token);
                        
                        _calculator = summary.Best.Calculator;
                        _statisticsAll = summary.Best.Statistics;
                        _regressionAll = summary.Best.Regression;
                    }
                    else
                    {
                        // Initialize the one calculator
                        var calc = usableCalculators.FirstOrDefault();
                        if (calc != null)
                        {
                            _regressionAll = RetentionTimeRegression.CalcSingleRegression(XmlNamedElement.NAME_INTERNAL,
                                calc,
                                targetTimes,
                                null,
                                true,
                                RegressionSettings.RegressionMethod,
                                out _statisticsAll,
                                out _,
                                token);
                        }
                        token.ThrowIfCancellationRequested();
                        _calculator = calc;
                    }

                    if (_calculator != null)
                    {
                        pointInfos = pointInfos.Select(pt =>
                            pt.ChangeX(_calculator.ScoreSequence(pt.ModifiedTarget))).ToList();
                    }
                }

                _regressionPredict = (IsRunToRun || RegressionSettings.RegressionMethod != RegressionMethodRT.linear)  ? null : document.Settings.PeptideSettings.Prediction.RetentionTime;
                if (_regressionPredict != null)
                {
                    if (!Equals(_calculator, _regressionPredict.Calculator))
                        _regressionPredict = null;
                    else
                    {
                        IDictionary<Target, double> scoreCache = null;
                        if (_regressionAll != null && Equals(_regressionAll.Calculator, _regressionPredict.Calculator))
                            scoreCache = _statisticsAll.ScoreCache;
                        // This is a bit of a HACK to better support the very common case of replicate graphing
                        // with a replicate that only has one file. More would need to be done for replicates
                        // composed of multiple files.
                        ChromFileInfoId fileId = null;
                        if (!RegressionSettings.BestResult && RegressionSettings.TargetIndex != -1)
                        {
                            var chromatogramSet = document.Settings.MeasuredResults.Chromatograms[RegressionSettings.TargetIndex];
                            if (chromatogramSet.FileCount > 0)
                            {
                                fileId = chromatogramSet.MSDataFileInfos[0].FileId;
                                _conversionPredict = _regressionPredict.GetConversion(fileId);
                            }
                        }
                        _statisticsPredict = _regressionPredict.CalcStatistics(targetTimes, scoreCache, fileId);
                    }
                }

                // Only refine, if not already exceeding the threshold
                _refine = refine && !IsRefined();
                _points = pointInfos.ToImmutable();
                _refinedPoints = _points.Where(pt=>pt.X.HasValue && pt.Y.HasValue).ToImmutable();
                _outlierPoints = _points.Where(pt => !pt.X.HasValue || !pt.Y.HasValue).ToImmutable();
                if (refine && !IsRefined())
                {
                    Refine(productionMonitor.CancellationToken);
                }
            }

            public SrmDocument Document
            {
                get { return RegressionSettings.Document; }
            }
            public RegressionSettings RegressionSettings { get; private set; }

            public bool IsRunToRun
            {
                get { return RegressionSettings.IsRunToRun; }
            }

            private float GetMaxQValue(PeptideDocNode node, int replicateIndex)
            {
                var chromInfos = node.TransitionGroups
                    .Select(tr => tr.GetSafeChromInfo(TargetIndex).FirstOrDefault(ci => ci.OptimizationStep == 0))
                    .Where(ci => ci?.QValue != null).ToArray();

                if (chromInfos.Length == 0)
                    return 1.0f;

                return chromInfos.Max(ci => ci.QValue.Value);
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

            private void Refine(CancellationToken cancellationToken)
            {
                // Now that we have added iRT calculators, RecalcRegression
                // cannot go and mark as outliers peptides at will anymore. It must know which peptides, if any,
                // are required by the calculator for a regression. With iRT calcs, the standard is required.
                if(!_calculator.IsUsable)
                    return;

                var outlierPoints = new List<PointInfo>();
                var validPoints = new List<PointInfo>();
                foreach (var pt in _points)
                {
                    if (pt.X.HasValue && pt.Y.HasValue)
                    {
                        validPoints.Add(pt);
                    }
                    else
                    {
                        outlierPoints.Add(pt);
                    }
                }
                var variableTargetPeptides = validPoints.Select(pt =>
                    new MeasuredRetentionTime(pt.ModifiedTarget, pt.Y.Value, true)).ToList();
                var variableOrigPeptides =
                    validPoints.Select(pt => new MeasuredRetentionTime(pt.ModifiedTarget, pt.X.Value, true)).ToList();

                var outlierIndexes = new HashSet<int>();
                //Throws DatabaseNotConnectedException
                RetentionTimeStatistics statisticsRefined = null;
                var regressionRefined = _regressionAll?.FindThreshold(RegressionSettings.Threshold,
                                                                         RegressionSettings.ThresholdPrecision,
                                                                         0,
                                                                         variableTargetPeptides.Count,
                                                                         Array.Empty<MeasuredRetentionTime>(),
                                                                         variableTargetPeptides,
                                                                         variableOrigPeptides,
                                                                         _statisticsAll,
                                                                         _calculator,
                                                                         RegressionSettings.RegressionMethod,
                                                                         null,
                                                                         cancellationToken, 
                                                                         ref statisticsRefined,
                                                                         ref outlierIndexes);

                if (ReferenceEquals(regressionRefined, _regressionAll))
                    return;

                _outlierPoints = outlierPoints.ToImmutable();
                _refinedPoints = Enumerable.Range(0, validPoints.Count).Where(i => !outlierIndexes.Contains(i))
                    .Select(i => validPoints[i]).ToImmutable();
                _regressionRefined = regressionRefined;
                _statisticsRefined = statisticsRefined;
            }

            public bool HasOutliers { get { return 0 < _outlierPoints.Count; } }
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
                get { return _regressionPredict ?? _regressionRefined ?? _regressionAll; }
            }

            private string ResidualsLabel
            {
                get
                {
                    if (IsRunToRun)
                    {
                        return string.Format(GraphsResources.GraphData_ResidualsLabel_Time_from_Regression___0__,
                            Document.MeasuredResults.Chromatograms[RegressionSettings.TargetIndex].Name);
                    }
                    else
                    {
                        return _regressionPredict != null
                            ? GraphsResources.GraphData_GraphResiduals_Time_from_Prediction
                            : Resources.GraphData_GraphResiduals_Time_from_Regression;
                    }
                }
            }

            private string CorrelationLabel
            {
                get
                {
                    if (IsRunToRun)
                    {
                        return string.Format(GraphsResources.GraphData_CorrelationLabel_Measured_Time___0__,
                            Document.MeasuredResults.Chromatograms[RegressionSettings.TargetIndex].Name);
                    }
                    else
                    {
                        return Resources.RTLinearRegressionGraphPane_RTLinearRegressionGraphPane_Measured_Time;
                    }
                }
            }

            private double[] GetResiduals(RetentionTimeRegression regression, double[] scores, double[] times)
            {
                var residualsRefined = new double[times.Length];
                for (int i = 0; i < residualsRefined.Length; i++)
                    residualsRefined[i] = GetResidual(regression, scores[i], times[i]);
                return residualsRefined;
            }

            public double GetResidual(RetentionTimeRegression regression, double score, double time)
            {
                //We round this for numerical error.
                return Math.Round(time - GetConversion(regression).GetY(score), 6);
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

                if (!_refine)
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

            private string XAxisName
            {
                get
                {
                    if (IsRunToRun)
                    {
                        if (Document.MeasuredResults != null && 0 <= RegressionSettings.OriginalIndex && RegressionSettings.OriginalIndex < Document.MeasuredResults.Chromatograms.Count)
                        {
                            return string.Format(GraphsResources.GraphData_CorrelationLabel_Measured_Time___0__,
                                Document.MeasuredResults.Chromatograms[RegressionSettings.OriginalIndex].Name);
                        }
                        return string.Empty;
                    }
                    return Calculator.Name;
                }
            }

            private string YAxisName
            {
                get
                {
                    if (RTGraphController.PlotType == PlotTypeRT.correlation)
                        return CorrelationLabel;
                    else
                        return ResidualsLabel;
                }
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

            string labelPoints = Helpers.PeptideToMoleculeTextMapper.Translate(GraphsResources.GraphData_Graph_Peptides, Data.Document.DocumentType);
            if (!Data.RegressionSettings.Refine)
            {
                GraphRegression(Data._statisticsAll, Data._regressionAll, GraphsResources.GraphData_Graph_Regression, COLOR_LINE_REFINED);
            }
            else
            {
                labelPoints = Helpers.PeptideToMoleculeTextMapper.Translate(GraphsResources.GraphData_Graph_Peptides_Refined, Data.Document.DocumentType);
                GraphRegression(Data._statisticsRefined, Data._regressionAll, GraphsResources.GraphData_Graph_Regression_Refined, COLOR_LINE_REFINED);
                GraphRegression(Data._statisticsAll, Data._regressionAll, GraphsResources.GraphData_Graph_Regression, COLOR_LINE_ALL);
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

            if (Data.Outliers != null)
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

            string labelPoints = Helpers.PeptideToMoleculeTextMapper.Translate(
                Data.IsRefined() ? GraphsResources.GraphData_Graph_Peptides_Refined : GraphsResources.GraphData_Graph_Peptides, Data.Document.DocumentType);
            var curve = AddCurve(labelPoints, Data.RefinedPoints.Select(Data.GetX).ToArray(),
                Data.RefinedPoints.Select(Data.GetYResidual).ToArray(), Color.Black, SymbolType.Diamond);
            curve.Line.IsVisible = false;
            curve.Symbol.Border.IsVisible = false;
            curve.Symbol.Fill = new Fill(COLOR_REFINED);

            if (Data.Outliers != null)
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
            public double? Y { get; }
        }

        private class RtRegressionResults : Immutable
        {
            public RtRegressionResults(RegressionSettings regressionSettings)
            {
                RegressionSettings = regressionSettings;
            }
            public RegressionSettings RegressionSettings { get; private set; }
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
        }

        private static Producer<RegressionSettings, RtRegressionResults> _producer = new DataProducer();
        private class DataProducer : Producer<RegressionSettings, RtRegressionResults>
        {
            public override RtRegressionResults ProduceResult(ProductionMonitor productionMonitor, RegressionSettings parameter, IDictionary<WorkOrder, object> inputs)
            {
                var results = new RtRegressionResults(parameter)
                    .ChangeInitializedCalculators(inputs.Values.OfType<InitializedRetentionScoreCalculator>());
                if (results.InitializedCalculators.Any(calc => calc.Initialized != null))
                {
                    return results;
                }

                return results.ChangeGraphData(new GraphData(parameter, productionMonitor));
            }

            public override IEnumerable<WorkOrder> GetInputs(RegressionSettings parameter)
            {
                foreach (var calculator in parameter.Calculators.Where(calc => !calc.IsUsable))
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
