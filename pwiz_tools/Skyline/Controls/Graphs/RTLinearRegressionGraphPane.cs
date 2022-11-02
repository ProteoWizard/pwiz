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
using System.Threading;
using System.Windows.Forms;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.RetentionTimes;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using ZedGraph;
using pwiz.Skyline.Util.Extensions;

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
        private CancellationTokenSource _cancellationTokenSource;
        public IProgressBar _progressBar;

        private bool _pendingUpdate;

        public RTLinearRegressionGraphPane(GraphSummary graphSummary, bool runToRun)
            : base(graphSummary)
        {
            XAxis.Title.Text = Resources.RTLinearRegressionGraphPane_RTLinearRegressionGraphPane_Score;
            RunToRun = runToRun;
            Settings.Default.RTScoreCalculatorList.ListChanged += RTScoreCalculatorList_ListChanged;
            AllowDisplayTip = true;
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public void Dispose()
        {
            Cancel(false);
            AllowDisplayTip = false;
            Settings.Default.RTScoreCalculatorList.ListChanged -= RTScoreCalculatorList_ListChanged;
            ProgressMonitor.TerminateProgressBar(_cancellationTokenSource.Token);
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
            // Avoid updating on every minor change to the list.
            if (_pendingUpdate)
                return;

            // Wait for the UI thread to become available again, and then update
            if (GraphSummary.IsHandleCreated)
            {
                GraphSummary.BeginInvoke(new Action(DelayedUpdate));
                _pendingUpdate = true;
            }
        }

        private void DelayedUpdate()
        {
            // Any change to the calculator list requires a full data update when in auto mode.
            if (string.IsNullOrEmpty(Settings.Default.RTCalculatorName))
                Data = null;

            UpdateGraph(true);
            _pendingUpdate = false;
        }

        public override bool HandleMouseMoveEvent(ZedGraphControl sender, MouseEventArgs e)
        {
            var peptideIndex = PeptideIndexFromPoint(new PointF(e.X, e.Y));
            if (peptideIndex != null)
            {
                double x, y;
                PointFromPeptide(peptideIndex.DocNode, out x, out y);
                if (RTGraphController.PlotType == PlotTypeRT.residuals && Data != null &&
                    Data.ResidualsRegression != null && Data.ResidualsRegression.Conversion != null)
                    y = Data.GetResidual(Data.ResidualsRegression, x, y);

                if (_tip == null)
                    _tip = new NodeTip(this);

                _tip.SetTipProvider(
                    new PeptideRegressionTipProvider(peptideIndex.DocNode, XAxis.Title.Text, YAxis.Title.Text,
                        new PointD(x, y)),
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
                var document = GraphSummary.DocumentUIContainer.DocumentUI;
                var pathSelect = document.GetPathTo((int) SrmDocument.Level.Molecules,
                                                    peptideIndex.IndexDoc);
                SelectPeptide(pathSelect);
                return true;
            }
            return false;
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
                GraphData data = Data;
                return data == null ? null : data.Outliers;
            }
        }

        public static PeptideDocNode[] CalcOutliers(SrmDocument document, double threshold, int? precision, bool bestResult)
        {
            var data = new GraphData(document, null, -1, threshold, precision, true, bestResult,
                RTGraphController.PointsType, RTGraphController.RegressionMethod, -1, null, CancellationToken.None);
            return data.Refine(CancellationToken.None).Outliers;
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

        private static bool IsValidFor(GraphData data, SrmDocument document)
        {
            return data != null && data.IsValidFor(document);
        }

        public bool IsValidFor(SrmDocument document, int targetIndex, int originalIndex, bool bestResult, double threshold, bool refine, PointsTypeRT pointsType, RegressionMethodRT regressionMethod)
        {
            var data = Data;
            return data != null && data.IsValidFor(document, targetIndex, originalIndex,bestResult, threshold, refine, pointsType, regressionMethod);
        }

        public void Clear()
        {
            Data = null;
            Title.Text = string.Empty;
            CurveList.Clear();
            GraphObjList.Clear();
        }

        public void Graph(PeptideDocNode nodeSelected)
        {
            var data = Data;
            if (data != null)
                data.Graph(this, nodeSelected);
        }

        private GraphData Update(SrmDocument document, int targetIndex, double threshold, bool refine, PointsTypeRT pointsType, RegressionMethodRT regressionMethod, int origIndex, CancellationToken token)
        {
            bool bestResults = (ShowReplicate == ReplicateDisplay.best);
            return new GraphData(document, Data, targetIndex, threshold, null, refine, bestResults, 
                pointsType, regressionMethod, origIndex, this, token);
            
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

        private GraphData Refine(GraphData currentData, CancellationToken cancellationToken)
        {
            GraphData dataNew = currentData != null ? currentData.Refine(cancellationToken) : null;

            // No refinement happened, if data did not change
            if (ReferenceEquals(dataNew, currentData))
                return currentData;

            return dataNew;
        }

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

        public PeptideDocumentIndex PeptideIndexFromPoint(PointF point)
        {
            var data = Data;
            return data != null ? data.PeptideIndexFromPoint(this, point) : null;
        }

        public bool PointFromPeptide(PeptideDocNode peptide, out double score, out double time)
        {
            score = time = 0;
            return Data != null && Data.PointFromPeptide(peptide, out score, out time);
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

        private void Cancel(bool createNew = true)
        {
            if (_cancellationTokenSource == null)
                return;

            _cancellationTokenSource.Cancel();

            if (createNew)
                _cancellationTokenSource = new CancellationTokenSource();
        }

        public override void UpdateGraph(bool selectionChanged)
        {
            GraphHelper.FormatGraphPane(this);
            SrmDocument document = GraphSummary.DocumentUIContainer.DocumentUI;
            PeptideDocNode nodeSelected = null;
            int targetIndex = (ShowReplicate == ReplicateDisplay.single || RunToRun ? GraphSummary.TargetResultsIndex : -1);
            int originalIndex = RunToRun ? GraphSummary.OriginalResultsIndex : -1;
            var results = document.Settings.MeasuredResults;
            bool resultsAvailable = results != null;
            if (resultsAvailable)
            {
                if (targetIndex == -1)
                    resultsAvailable = results.IsLoaded;
                else
                    resultsAvailable = results.Chromatograms.Count > targetIndex &&
                                       results.IsChromatogramSetLoaded(targetIndex);
            }

            if (RunToRun && originalIndex < 0)
            {
                resultsAvailable = false;
            }

            if (!resultsAvailable)
            {
                Clear();
            }
            else
            {
                GraphObjList.Clear();
                var nodeTree = GraphSummary.StateProvider.SelectedNode as SrmTreeNode;
                var nodePeptide = nodeTree as PeptideTreeNode;
                while (nodePeptide == null && nodeTree != null)
                {
                    nodeTree = nodeTree.Parent as SrmTreeNode;
                    nodePeptide = nodeTree as PeptideTreeNode;
                }
                if (nodePeptide != null)
                    nodeSelected = nodePeptide.DocNode;

                bool shouldDrawGraph = true;

                double threshold = RTGraphController.OutThreshold;
                bool refine = Settings.Default.RTRefinePeptides && RTGraphController.CanDoRefinementForRegressionMethod;

                bool bestResult = (ShowReplicate == ReplicateDisplay.best);
                    
                if ((RTGraphController.PointsType == PointsTypeRT.standards && !document.GetRetentionTimeStandards().Any()) ||
                    (RTGraphController.PointsType == PointsTypeRT.decoys &&
                        !document.PeptideGroups.Any(nodePepGroup => nodePepGroup.Children.Cast<PeptideDocNode>().Any(nodePep => nodePep.IsDecoy))) ||
                    RTGraphController.PointsType == PointsTypeRT.targets_fdr && targetIndex == -1) // Replicate display is not single and this is not a run to run regression
                {
                    RTGraphController.PointsType = PointsTypeRT.targets;
                }

                PointsTypeRT pointsType = RTGraphController.PointsType;
                RegressionMethodRT regressionMethod = RTGraphController.RegressionMethod;

                if (!IsValidFor(document, targetIndex, originalIndex, bestResult, threshold, refine, pointsType,
                    regressionMethod))
                {
                    var requested = new RequestContext(new RegressionSettings(document, targetIndex, originalIndex, bestResult,
                        threshold, refine, pointsType, regressionMethod, Settings.Default.RTCalculatorName, RunToRun));
                    if (UpdateData(requested))
                    {
                        // Calculate and refine regression on background thread
                        lock (_requestLock)
                        {
                            // 
                            var ctx = _requestContext;
                            var token = _cancellationTokenSource.Token;
                            var decoyCount = document.Molecules.Count((m) => m.IsDecoy);
                            var calcCount = Settings.Default.RTCalculatorName.IsNullOrEmpty()
                                ? Settings.Default.RTScoreCalculatorList.Count
                                : 1;
                            var maxCount = 0;
                            if (calcCount == 1)
                                maxCount = document.MoleculeCount + (document.MoleculeCount - decoyCount) * 2;
                            else
                                maxCount = document.MoleculeCount + (document.MoleculeCount - decoyCount) * calcCount;

                            _progressBar = ProgressMonitor.RegisterProgressBar(token, maxCount
                                , 1, new PaneProgressBar(this));

                            ActionUtil.RunAsync(() => UpdateAndRefine(ctx, token),
                                @"Update and refine regression data");
                        }
                        Title.Text = Resources.RTLinearRegressionGraphPane_UpdateGraph_Calculating___;
                        shouldDrawGraph = false;
                        Legend.IsVisible = false;
                    }
                }
                else
                {
                    lock (_requestLock)
                    {
                        _requestContext = null;
                        ProgressMonitor.TerminateProgressBar(_cancellationTokenSource.Token);
                        _progressBar = null;
                        Legend.IsVisible = true;
                    }
                }

                if (shouldDrawGraph)
                    Graph(nodeSelected);
            }

            lock (_requestLock)
            {
                if (_requestContext?.Settings == null)
                    Title.Text = string.Empty;
            }

            AxisChange();
            GraphSummary.GraphControl.Invalidate();
        }

        // Returns true if data should be updated
        bool UpdateData(RequestContext requested)
        {
            lock (_requestLock)
            {
                if (_requestContext?.Settings == null)
                {
                    _requestContext = requested;
                    return true;
                }
                else
                {
                    var valid = _requestContext.Settings.IsValidFor(requested.Settings);
                    if (!valid)
                    {
                        Cancel();
                        _requestContext = requested;
                    }

                    return !valid;
                }
            }
        }

        private class RequestContext
        {
            public RequestContext(RegressionSettings requested)
            {
                Settings = requested;
            }

            public RegressionSettings Settings { get; set; }
        }

        private RequestContext _requestContext;
        private readonly object _requestLock = new object();
        private bool _allowDisplayTip;

        public bool IsCalculating
        {
            get
            {
                lock (_requestLock)
                {
                    return _requestContext?.Settings != null;
                }
            }
        }

        private class RegressionSettings
        {
            public 
                RegressionSettings(SrmDocument document, int targetIndex, int originalIndex, bool bestResult,
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
                    Calculators = new[] {Settings.Default.GetCalculatorByName(calculatorName)};
                else
                    Calculators = Settings.Default.RTScoreCalculatorList.ToArray();
                IsRunToRun = isRunToRun;
            }

            public bool IsValidFor(RegressionSettings other)
            {
                return IsValidFor(other.Document, other.TargetIndex, other.OriginalIndex, other.BestResult,
                    other.Threshold, other.Refine, other.PointsType, other.RegressionMethod, other.CalculatorName, other.IsRunToRun);
            }

            private bool IsValidFor(SrmDocument document, int targetIndex, int originalIndex, bool bestResult,
                double threshold, bool refine, PointsTypeRT pointsType, RegressionMethodRT regressionMethod, string calculatorName, bool isRunToRun)
            {
                if(!(ReferenceEquals(Document, document) && TargetIndex == targetIndex &&
                            OriginalIndex == originalIndex && BestResult == bestResult && Threshold == threshold &&
                            Refine == refine && PointsType == pointsType && RegressionMethod == regressionMethod &&
                            IsRunToRun == isRunToRun))
                    return false;

                if (!IsRunToRun)
                {
                    if (string.IsNullOrEmpty(calculatorName))
                        return ArrayUtil.EqualsDeep(Calculators, Settings.Default.RTScoreCalculatorList);
                    else
                        return CalculatorName == calculatorName && Equals(Calculators[0],
                                   Settings.Default.GetCalculatorByName(calculatorName));
                }

                return true;
            }

            public SrmDocument Document { get; private set; }
            public int TargetIndex { get; private set; }
            public int OriginalIndex { get; private set; }
            public bool BestResult { get; private set; }
            public double Threshold { get; private set; }
            public bool Refine { get; private set; }
            public PointsTypeRT PointsType { get; private set; }
            public RegressionMethodRT RegressionMethod { get; private set; }
            public string CalculatorName { get; private set; }
            public RetentionScoreCalculatorSpec[] Calculators { get; private set; }
            public bool IsRunToRun { get; private set; }
        }

        private void UpdateAndRefine(RequestContext requestContext,
            CancellationToken cancellationToken)
        {
            try
            {
                var regressionSettings = requestContext.Settings;
                var newData = Update(regressionSettings.Document, regressionSettings.TargetIndex,
                    regressionSettings.Threshold,
                    regressionSettings.Refine, regressionSettings.PointsType, regressionSettings.RegressionMethod,
                    regressionSettings.OriginalIndex,
                    // ReSharper disable once InconsistentlySynchronizedField
                    cancellationToken);

                if (regressionSettings.Refine && !IsDataRefined(newData))
                {
                    var data = newData;
                    using (var cancellationTokenSource = new DocumentCancellationToken(cancellationToken,
                        GraphSummary.DocumentUIContainer, doc => !IsValidFor(data, doc)))
                    {
                        newData = Refine(newData, cancellationTokenSource.Token);
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();

                // Update the graph on the UI thread.
                lock (_requestLock)
                {
                    if (ReferenceEquals(_requestContext, requestContext))
                    {
                        Interlocked.CompareExchange(ref _data, newData, Data);
                    }

                    // Set to null so that the next UpdateGraph call will update graph title accordingly
                    requestContext.Settings = null;
                }

                if (GraphSummary.IsHandleCreated)
                {
                    try
                    {
                        GraphSummary.Invoke(new Action(() =>
                        {
                            try
                            {
                                if (!cancellationToken.IsCancellationRequested)
                                    UpdateGraph(false);
                            }
                            catch (Exception ex)
                            {
                                Program.ReportException(ex);
                            }
                        }));
                    }
                    catch (ObjectDisposedException)
                    {
                        // Can happen during tests
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception x)
            {
                Program.ReportException(x);
            }

            lock (_requestLock)
            {
                requestContext.Settings = null;
            }
        }

        /// <summary>
        /// Holds the data currently displayed in the graph.
        /// </summary>
        sealed class GraphData : Immutable
        {
            public const int REPORTING_STEP = 3;

            private readonly SrmDocument _document;
            private readonly RTLinearRegressionGraphPane _graphPane;
            private readonly RegressionMethodRT _regressionMethod;
            private readonly int _targetIndex;
            private readonly int _originalIndex; // set to -1 if we are using IRTs
            private readonly bool _bestResult;
            private readonly double _threshold;
            private readonly int? _thresholdPrecision;
            private readonly bool _refine;
            private readonly PointsTypeRT _pointsType;
            private readonly List<PeptideDocumentIndex> _peptidesIndexes;
            private readonly List<MeasuredRetentionTime> _targetTimes;
            private readonly IList<MeasuredRetentionTime> _originalTimes;

            private readonly RetentionTimeScoreCache _scoreCache;

            private readonly RetentionTimeRegression _regressionPredict;
            private readonly IRegressionFunction _conversionPredict;
            private readonly RetentionTimeStatistics _statisticsPredict;

            private readonly RetentionTimeRegression _regressionAll;
            private readonly RetentionTimeStatistics _statisticsAll;

            private RetentionTimeRegression _regressionRefined;
            private RetentionTimeStatistics _statisticsRefined;

            private double[] _timesRefined;
            private double[] _scoresRefined;
            private double[] _timesOutliers;
            private double[] _scoresOutliers;
            private readonly string _calculatorName;

            private readonly RetentionScoreCalculatorSpec _calculator;

            private RetentionScoreCalculatorSpec Calculator { get { return _calculator; } }

            private HashSet<int> _outlierIndexes;

            private bool IsRunToRun { get { return _graphPane != null && _graphPane.RunToRun; }  }

            public GraphData(SrmDocument document,
                GraphData dataPrevious,
                int targetIndex,
                double threshold,
                int? thresholdPrecision,
                bool refine,
                bool bestResult,
                PointsTypeRT pointsType,
                RegressionMethodRT regressionMethod,
                int originalIndex,
                RTLinearRegressionGraphPane graphPane,
                CancellationToken token
                )
            {
                _document = document;
                _graphPane = graphPane;
                _targetIndex = targetIndex;
                _originalIndex = originalIndex;
                if(IsRunToRun && _originalIndex < 0)
                    throw new ArgumentException(@"Original index cannot not be negative if we are doing run to run regression");
                _bestResult = bestResult && !IsRunToRun;
                _threshold = threshold;
                _thresholdPrecision = thresholdPrecision;
                _pointsType = pointsType;
                _regressionMethod = regressionMethod;
                _peptidesIndexes = new List<PeptideDocumentIndex>();
                _targetTimes = new List<MeasuredRetentionTime>();
                var originalTimes = IsRunToRun ? new List<MeasuredRetentionTime>() : null; 
                int index = -1;

                var standards = new HashSet<Target>();
                if (RTGraphController.PointsType == PointsTypeRT.standards)
                    standards = document.GetRetentionTimeStandards();
                
                // Only used if we are comparing two runs
                var origTimesDict = IsRunToRun ? new Dictionary<Target, double>() : null;
                var targetTimesDict = IsRunToRun ? new Dictionary<Target, double>() : null;

                var reportingStep = document.PeptideCount / (90 / REPORTING_STEP);
                foreach (var nodePeptide in document.Molecules)
                {
                    ProgressMonitor.CheckCanceled(token);
                    index++;
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

                    if (originalIndex != -1)
                        rtOrig = nodePeptide.GetSchedulingTime(originalIndex);
                    
                    if (!_bestResult)
                        rtTarget = nodePeptide.GetSchedulingTime(targetIndex);
                    else
                    {
                        int iBest = nodePeptide.BestResult;
                        if (iBest != -1)
                            rtTarget = nodePeptide.GetSchedulingTime(iBest);
                    }

                    var modSeq = _document.Settings.GetSourceTarget(nodePeptide);
                    if (!rtTarget.HasValue)
                        rtTarget = 0;
                    if (!rtOrig.HasValue)
                        rtOrig = 0;
                    _peptidesIndexes.Add(new PeptideDocumentIndex(nodePeptide, index));
                    _targetTimes.Add(new MeasuredRetentionTime(modSeq, rtTarget.Value));
                    if(IsRunToRun)
                        originalTimes.Add(new MeasuredRetentionTime(modSeq,rtOrig.Value));

                    
                    if (IsRunToRun )
                    {
                        if (!targetTimesDict.ContainsKey(modSeq))
                        {
                            targetTimesDict.Add(modSeq, rtTarget.Value);
                            origTimesDict.Add(modSeq, rtOrig.Value);
                        }
                        else
                        {
                            _peptidesIndexes.RemoveAt(_peptidesIndexes.Count-1);
                            _targetTimes.RemoveAt(_targetTimes.Count-1);
                            originalTimes.RemoveAt(originalTimes.Count-1);
                        }
                    }
                }

                _originalTimes = originalTimes != null ? originalTimes.ToArray() : null;

                _calculatorName = Settings.Default.RTCalculatorName;

                if (IsRunToRun)
                {
                    _calculator = new DictionaryRetentionScoreCalculator(XmlNamedElement.NAME_INTERNAL, origTimesDict);
                    var alignedRetentionTimes = AlignedRetentionTimes.AlignLibraryRetentionTimes(targetTimesDict,
                        origTimesDict, refine ? threshold : 0, _regressionMethod,
                        token);
                    if (alignedRetentionTimes != null)
                    {
                        _regressionAll = alignedRetentionTimes.Regression;
                        _statisticsAll = alignedRetentionTimes.RegressionStatistics;
                    }
                }
                else
                {
                    var calc = !string.IsNullOrEmpty(_calculatorName)
                        ? Settings.Default.GetCalculatorByName(Settings.Default.RTCalculatorName)
                        : null;
                    if (calc == null)
                    {
                        // Initialize all calculators
                        Settings.Default.RTScoreCalculatorList.Initialize(null);

                        var summary = RetentionTimeRegression.CalcBestRegressionBackground(XmlNamedElement.NAME_INTERNAL,
                            Settings.Default.RTScoreCalculatorList.ToList(), _targetTimes, _scoreCache, true,
                            _regressionMethod, token);
                        
                        _calculator = summary.Best.Calculator;
                        _statisticsAll = summary.Best.Statistics;
                        _regressionAll = summary.Best.Regression;
                    }
                    else
                    {
                        // Initialize the one calculator
                        calc = Settings.Default.RTScoreCalculatorList.Initialize(null, calc);

                        double unused;
                        _regressionAll = RetentionTimeRegression.CalcSingleRegression(XmlNamedElement.NAME_INTERNAL,
                            calc,
                            _targetTimes,
                            _scoreCache,
                            true,
                            _regressionMethod,
                            out _statisticsAll,
                            out unused,
                            token);

                        token.ThrowIfCancellationRequested();
                        _calculator = calc;

                        //If _regressionAll is null, it is safe to assume that the calculator is an iRT Calc with
                        //its database disconnected.
                        if (_regressionAll == null)
                        {
                            var tryIrtCalc = calc as RCalcIrt;
                            //Only show an error message if the user specifically chooses this calculator.
                            if (dataPrevious != null && !ReferenceEquals(calc, dataPrevious.Calculator) &&
                                tryIrtCalc != null)
                            {
                                MessageDlg.Show(Program.MainWindow, string.Format(
                                    Resources.GraphData_GraphData_The_database_for_the_calculator__0__could_not_be_opened__Check_that_the_file__1__was_not_moved_or_deleted_,
                                    tryIrtCalc.Name, tryIrtCalc.DatabasePath));
                                return;
                            }
                        }
                    }
                }

                if (_regressionAll != null)
                {
                    _scoreCache = new RetentionTimeScoreCache(new[] { _calculator }, _targetTimes,
                                                              dataPrevious != null ? dataPrevious._scoreCache : null);

                    if (dataPrevious != null && !ReferenceEquals(_calculator, dataPrevious._calculator))
                        _scoreCache.RecalculateCalcCache(_calculator, token);

                    _scoresRefined = _statisticsAll.ListHydroScores.ToArray();
                    _timesRefined = _statisticsAll.ListRetentionTimes.ToArray();
                }

                _regressionPredict = (IsRunToRun || _regressionMethod != RegressionMethodRT.linear)  ? null : document.Settings.PeptideSettings.Prediction.RetentionTime;
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
                        if (!bestResult && targetIndex != -1)
                        {
                            var chromatogramSet = document.Settings.MeasuredResults.Chromatograms[targetIndex];
                            if (chromatogramSet.FileCount > 0)
                            {
                                fileId = chromatogramSet.MSDataFileInfos[0].FileId;
                                _conversionPredict = _regressionPredict.GetConversion(fileId);
                            }
                        }
                        _statisticsPredict = _regressionPredict.CalcStatistics(_targetTimes, scoreCache, fileId);
                    }
                }

                // Only refine, if not already exceeding the threshold
                _refine = refine && !IsRefined();
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

            public bool IsValidFor(SrmDocument document)
            {
                return ReferenceEquals(document, _document);
            }

            public bool IsValidFor(SrmDocument document, int targetIndex, int originalIndex, bool bestResult, double threshold, bool refine, PointsTypeRT pointsType, RegressionMethodRT regressionMethod)
            {
                string calculatorName = Settings.Default.RTCalculatorName;
                if (string.IsNullOrEmpty(calculatorName) && !IsRunToRun)
                    calculatorName = _calculator.Name;
                return IsValidFor(document) &&
                        _targetIndex == targetIndex &&
                        _originalIndex == originalIndex &&
                        _bestResult == bestResult &&
                        _threshold == threshold &&
                        _pointsType == pointsType &&
                        _regressionMethod == regressionMethod && 
                        (IsRunToRun || (_calculatorName == Settings.Default.RTCalculatorName &&
                        ReferenceEquals(_calculator, Settings.Default.GetCalculatorByName(calculatorName)))) &&
                        // Valid if refine is true, and this data requires no further refining
                        (_refine == refine || (refine && IsRefined()));
            }

            public int TargetIndex { get { return _targetIndex; } }

            public int OriginalIndex { get { return _originalIndex; } }

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
                return RetentionTimeRegression.IsAboveThreshold(_statisticsAll.R, _threshold);
            }

            public GraphData Refine(CancellationToken cancellationToken)
            {
                if (IsRefined())
                    return this;
                var result = ImClone(this).RefineCloned(_threshold, _thresholdPrecision, cancellationToken);
                if (result == null)
                    return this;
                return result;
            }

            private GraphData RefineCloned(double threshold, int? precision, CancellationToken cancellationToken)
            {
                // Create list of deltas between predicted and measured times
                _outlierIndexes = new HashSet<int>();
                // Start with anything assigned a zero retention time as outliers
                for (int i = 0; i < _targetTimes.Count; i++)
                {
                    if (_targetTimes[i].RetentionTime == 0 || (_originalTimes != null && _originalTimes[i].RetentionTime == 0))
                        _outlierIndexes.Add(i);
                }

                // Now that we have added iRT calculators, RecalcRegression
                // cannot go and mark as outliers peptides at will anymore. It must know which peptides, if any,
                // are required by the calculator for a regression. With iRT calcs, the standard is required.
                if(!_calculator.IsUsable)
                    return null;

                HashSet<Target> standardNames;
                try
                {
                    var names = _calculator.GetStandardPeptides(_targetTimes.Select(pep => pep.PeptideSequence));
                    standardNames = new HashSet<Target>(names);
                }
                catch (CalculatorException)
                {
                    standardNames = new HashSet<Target>();
                }

                //For run to run all peptides are variables. There are no standards.
                var standardPeptides = IsRunToRun ? new MeasuredRetentionTime[0] : _targetTimes.Where(pep => pep.IsStandard && standardNames.Contains(pep.PeptideSequence)).ToArray();
                var variableTargetPeptides = IsRunToRun ? _targetTimes.ToArray() : _targetTimes.Where(pep => !pep.IsStandard || !standardNames.Contains(pep.PeptideSequence)).ToArray();
                var variableOrigPeptides = _originalTimes;

                //Throws DatabaseNotConnectedException
                _regressionRefined = (_regressionAll == null
                                          ? null
                                          : _regressionAll.FindThreshold(threshold,
                                                                         precision,
                                                                         0,
                                                                         variableTargetPeptides.Length,
                                                                         standardPeptides,
                                                                         variableTargetPeptides,
                                                                         variableOrigPeptides,
                                                                         _statisticsAll,
                                                                         _calculator,
                                                                         _regressionMethod,
                                                                         _scoreCache,
                                                                         cancellationToken, 
                                                                         ref _statisticsRefined,
                                                                         ref _outlierIndexes));

                if (ReferenceEquals(_regressionRefined, _regressionAll))
                    return null;

                // Separate lists into acceptable and outliers
                var listScoresRefined = new List<double>();
                var listTimesRefined = new List<double>();
                var listScoresOutliers = new List<double>();
                var listTimesOutliers = new List<double>();
                for (int i = 0; i < _scoresRefined.Length; i++)
                {
                    if (_outlierIndexes.Contains(i))
                    {
                        listScoresOutliers.Add(_scoresRefined[i]);
                        listTimesOutliers.Add(_timesRefined[i]);
                    }
                    else
                    {
                        listScoresRefined.Add(_scoresRefined[i]);
                        listTimesRefined.Add(_timesRefined[i]);
                    }
                }
                _scoresRefined = listScoresRefined.ToArray();
                _timesRefined = listTimesRefined.ToArray();
                _scoresOutliers = listScoresOutliers.ToArray();
                _timesOutliers = listTimesOutliers.ToArray();

                return this;
            }

            public PeptideDocumentIndex PeptideIndexFromPoint(RTLinearRegressionGraphPane graphPane, PointF point)
            {
                var regression = ResidualsRegression;
                if (RTGraphController.PlotType == PlotTypeRT.correlation)
                    regression = null;
                if (RTGraphController.PlotType == PlotTypeRT.correlation || regression != null)
                {
                    int iRefined = 0, iOut = 0;
                    for (int i = 0; i < _peptidesIndexes.Count; i++)
                    {
                        if (_outlierIndexes != null && _outlierIndexes.Contains(i))
                        {
                            if (PointIsOverEx(graphPane, point, regression, _scoresOutliers[iOut], _timesOutliers[iOut]))
                                return _peptidesIndexes[i];
                            iOut++;
                        }
                        else if (_scoresRefined != null && _timesRefined != null)
                        {
                            if (PointIsOverEx(graphPane, point, regression, _scoresRefined[iRefined], _timesRefined[iRefined]))
                                return _peptidesIndexes[i];
                            iRefined++;
                        }
                    }
                }
                return null;
            }

            private bool PointIsOverEx(RTLinearRegressionGraphPane graphPane, PointF point,
                RetentionTimeRegression regression, double x, double y)
            {
                if (regression != null && regression.IsUsable)
                    y = GetResidual(regression, x, y);
                return graphPane.PointIsOver(point, x, y);
            }

            public bool PointFromPeptide(PeptideDocNode nodePeptide, out double score, out double time)
            {
                if (nodePeptide != null && _regressionAll != null)
                {
                    int iRefined = 0, iOut = 0;
                    for (int i = 0; i < _peptidesIndexes.Count; i++)
                    {
                        if (_outlierIndexes != null && _outlierIndexes.Contains(i))
                        {
                            if (ReferenceEquals(nodePeptide, _peptidesIndexes[i].DocNode))
                            {
                                score = _scoresOutliers[iOut];
                                time = _timesOutliers[iOut];
                                return true;
                            }
                            iOut++;
                        }
                        else
                        {
                            if (ReferenceEquals(nodePeptide, _peptidesIndexes[i].DocNode))
                            {
                                score = _scoresRefined[iRefined];
                                time = _timesRefined[iRefined];
                                return true;
                            }
                            iRefined++;
                        }
                    }
                }
                score = 0;
                time = 0;
                return false;
            }

            public bool HasOutliers { get { return _outlierIndexes != null && _outlierIndexes.Count > 0; } }

            public PeptideDocNode[] Outliers
            {
                get
                {
                    if (!HasOutliers)
                        return new PeptideDocNode[0];

                    var listOutliers = new List<PeptideDocNode>();
                    for (int i = 0; i < _peptidesIndexes.Count; i++)
                    {
                        if (_outlierIndexes.Contains(i))
                            listOutliers.Add(_peptidesIndexes[i].DocNode);
                    }
                    return listOutliers.ToArray();
                }
            }

            public void Graph(GraphPane graphPane, PeptideDocNode nodeSelected)
            {
                graphPane.CurveList.Clear();
                graphPane.XAxis.Title.Text = XAxisName;
                graphPane.YAxis.Title.Text = YAxisName;
                if (RTGraphController.PlotType == PlotTypeRT.correlation)
                    GraphCorrelation(graphPane, nodeSelected);
                else
                    GraphResiduals(graphPane, nodeSelected);
            }

            private void GraphCorrelation(GraphPane graphPane, PeptideDocNode nodeSelected)
            {
                if (graphPane.YAxis.Scale.MinAuto)
                {
                    graphPane.YAxis.Scale.MinAuto = false;
                    graphPane.YAxis.Scale.Min = 0;
                }

                double scoreSelected, timeSelected;
                if (PointFromPeptide(nodeSelected, out scoreSelected, out timeSelected))
                {
                    Color colorSelected = GraphSummary.ColorSelected;
                    var curveOut = graphPane.AddCurve(null, new[] { scoreSelected }, new[] { timeSelected },
                                                      colorSelected, SymbolType.Diamond);
                    curveOut.Line.IsVisible = false;
                    curveOut.Symbol.Fill = new Fill(colorSelected);
                    curveOut.Symbol.Size = 8f;
                }

                string labelPoints = Resources.GraphData_Graph_Peptides;
                if (!_refine)
                {
                    GraphRegression(graphPane, _statisticsAll, _regressionAll, Resources.GraphData_Graph_Regression, COLOR_LINE_REFINED);
                }
                else
                {
                    labelPoints = Resources.GraphData_Graph_Peptides_Refined;
                    GraphRegression(graphPane, _statisticsRefined, _regressionAll, Resources.GraphData_Graph_Regression_Refined, COLOR_LINE_REFINED);
                    GraphRegression(graphPane, _statisticsAll, _regressionAll, Resources.GraphData_Graph_Regression, COLOR_LINE_ALL);
                }

                if (_regressionPredict != null && Settings.Default.RTPredictorVisible)
                {
                    GraphRegression(graphPane, _statisticsPredict, _regressionAll, Resources.GraphData_Graph_Predictor, COLOR_LINE_PREDICT);
                }

                var curve = graphPane.AddCurve(labelPoints, _scoresRefined, _timesRefined,
                                               Color.Black, SymbolType.Diamond);
                curve.Line.IsVisible = false;
                curve.Symbol.Border.IsVisible = false;
                curve.Symbol.Fill = new Fill(COLOR_REFINED);

                if (_scoresOutliers != null)
                {
                    var curveOut = graphPane.AddCurve(Resources.GraphData_Graph_Outliers, _scoresOutliers, _timesOutliers,
                                                      Color.Black, SymbolType.Diamond);
                    curveOut.Line.IsVisible = false;
                    curveOut.Symbol.Border.IsVisible = false;
                    curveOut.Symbol.Fill = new Fill(COLOR_OUTLIERS);
                }
            }

            private void GraphResiduals(GraphPane graphPane, PeptideDocNode nodeSelected)
            {
                if (!graphPane.YAxis.Scale.MinAuto && graphPane.ZoomStack.Count == 0)
                {
                    graphPane.YAxis.Scale.MinAuto = true;
                    graphPane.YAxis.Scale.MaxAuto = true;
                }

                var regression = ResidualsRegression;
                if (regression == null || regression.Conversion == null)
                    return;

                double scoreSelected, timeSelected;
                if (PointFromPeptide(nodeSelected, out scoreSelected, out timeSelected))
                {
                    timeSelected = GetResidual(regression, scoreSelected, timeSelected);

                    Color colorSelected = GraphSummary.ColorSelected;
                    var curveOut = graphPane.AddCurve(null, new[] { scoreSelected }, new[] { timeSelected },
                                                      colorSelected, SymbolType.Diamond);
                    curveOut.Line.IsVisible = false;
                    curveOut.Symbol.Fill = new Fill(colorSelected);
                    curveOut.Symbol.Size = 8f;
                }

                string labelPoints = _refine ? Resources.GraphData_Graph_Peptides_Refined : Resources.GraphData_Graph_Peptides;
                var curve = graphPane.AddCurve(labelPoints, _scoresRefined, GetResiduals(regression, _scoresRefined, _timesRefined),
                                               Color.Black, SymbolType.Diamond);
                curve.Line.IsVisible = false;
                curve.Symbol.Border.IsVisible = false;
                curve.Symbol.Fill = new Fill(COLOR_REFINED);

                if (_scoresOutliers != null)
                {
                    var curveOut = graphPane.AddCurve(Resources.GraphData_Graph_Outliers, _scoresOutliers, 
                                                      GetResiduals(regression, _scoresOutliers, _timesOutliers),
                                                      Color.Black, SymbolType.Diamond);
                    curveOut.Line.IsVisible = false;
                    curveOut.Symbol.Border.IsVisible = false;
                    curveOut.Symbol.Fill = new Fill(COLOR_OUTLIERS);
                }
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
                        return string.Format(Resources.GraphData_ResidualsLabel_Time_from_Regression___0__,
                            _document.MeasuredResults.Chromatograms[_targetIndex].Name);
                    }
                    else
                    {
                        return _regressionPredict != null
                            ? Resources.GraphData_GraphResiduals_Time_from_Prediction
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
                        return string.Format(Resources.GraphData_CorrelationLabel_Measured_Time___0__,
                            _document.MeasuredResults.Chromatograms[_targetIndex].Name);
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

            private static void GraphRegression(GraphPane graphPane,
                                                RetentionTimeStatistics statistics, RetentionTimeRegression regression, string name, Color color)
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
                var curve = graphPane.AddCurve(name, lineScores, lineTimes, color, SymbolType.None);
                if (lineScores.Length > 0 && lineTimes.Length > 0)
                {
                    graphPane.AddCurve(string.Empty, new[] { lineScores[0] }, new[] { lineTimes[0] }, color, SymbolType.Square);
                    graphPane.AddCurve(string.Empty, new[] { lineScores.Last() }, new[] { lineTimes.Last() }, color, SymbolType.Square);
                }

                curve.Line.IsAntiAlias = true;
                curve.Line.IsOptimizedDraw = true;
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
                        if (_document.MeasuredResults != null && 0 <= _originalIndex && _originalIndex < _document.MeasuredResults.Chromatograms.Count)
                        {
                            return string.Format(Resources.GraphData_CorrelationLabel_Measured_Time___0__,
                                _document.MeasuredResults.Chromatograms[_originalIndex].Name);
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
    }

    public sealed class PeptideDocumentIndex
    {
        public PeptideDocumentIndex(PeptideDocNode docNode, int indexDoc)
        {
            DocNode = docNode;
            IndexDoc = indexDoc;
        }

        public PeptideDocNode DocNode { get; private set; }
        public int IndexDoc { get; private set; }
    }
}
