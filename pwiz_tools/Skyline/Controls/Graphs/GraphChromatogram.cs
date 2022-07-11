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
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using pwiz.Common.SystemUtil;
using pwiz.MSGraph;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Model.RetentionTimes;
using pwiz.Skyline.Model.Themes;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using ZedGraph;

namespace pwiz.Skyline.Controls.Graphs
{
    public enum ShowRTChrom { none, all, best, threshold }

    public enum AutoZoomChrom { none, peak, window, both }

    public enum DisplayTypeChrom { single, precursors, products, all, total, base_peak, tic, qc }

    public partial class GraphChromatogram : DockableFormEx, IGraphContainer
    {
        public const double DEFAULT_PEAK_RELATIVE_WINDOW = 3.4;

        public static ShowRTChrom ShowRT
        {
            get
            {
                return Helpers.ParseEnum(Settings.Default.ShowRetentionTimesEnum, ShowRTChrom.all);
            }
        }

        public static AutoZoomChrom AutoZoom
        {
            get
            {
                return Helpers.ParseEnum(Settings.Default.AutoZoomChromatogram, AutoZoomChrom.none);
            }
        }

        public static TransformChrom Transform
        {
            get
            {
                var transformType = Settings.Default.TransformTypeChromatogram;
                if (transformType == @"none")
                {
                    return TransformChrom.interpolated;
                }
                return Helpers.ParseEnum(transformType, TransformChrom.interpolated);
            }
        }

        public static DisplayTypeChrom DisplayType
        {
            get
            {
                return Helpers.ParseEnum(Settings.Default.ShowTransitionGraphs, DisplayTypeChrom.all);
            }
        }

        public static DisplayTypeChrom GetDisplayType(SrmDocument documentUI, SrmTreeNode selectedTreeNode)
        {
            TransitionGroupDocNode nodeGroup = null;
            var peptideTreeNode = SequenceTree.GetNodeOfType<PeptideTreeNode>(selectedTreeNode);
            if (peptideTreeNode != null && peptideTreeNode.DocNode.TransitionGroupCount == 1)
                nodeGroup = peptideTreeNode.DocNode.TransitionGroups.First();
            else
            {
                var transitionGroupTreeNode = SequenceTree.GetNodeOfType<TransitionGroupTreeNode>(selectedTreeNode);
                if (transitionGroupTreeNode != null)
                    nodeGroup = transitionGroupTreeNode.DocNode;
            }
            return GetDisplayType(documentUI, nodeGroup);
        }

        public static DisplayTypeChrom GetDisplayType(SrmDocument documentUI, TransitionGroupDocNode nodeGroup)
        {
            var displayType = GetDisplayType(documentUI);
            if (displayType == DisplayTypeChrom.products || displayType == DisplayTypeChrom.precursors)
            {
                if (!IsMultipleIonSources(documentUI.Settings.TransitionSettings.FullScan, nodeGroup))
                    displayType = DisplayTypeChrom.all;
            }
            return displayType;
        }

        public static DisplayTypeChrom GetDisplayType(SrmDocument documentUI)
        {
            var displayType = DisplayType;
            if (displayType == DisplayTypeChrom.base_peak || displayType == DisplayTypeChrom.tic || displayType == DisplayTypeChrom.qc)
            {
                if (!documentUI.Settings.HasResults || !documentUI.Settings.MeasuredResults.HasAllIonsChromatograms)
                    displayType = DisplayTypeChrom.all;
            }
            return displayType;
        }

        public static bool IsMultipleIonSources(TransitionFullScan fullScan, TransitionGroupDocNode nodeGroup)
        {
            return fullScan.IsEnabledMs &&
                   (fullScan.IsEnabledMsMs ||
                        (nodeGroup != null && nodeGroup.Transitions.Contains(nodeTran => !nodeTran.IsMs1)));
        }

        public static bool IsSingleTransitionDisplay
        {
            get { return DisplayType == DisplayTypeChrom.single; }
        }

        public static IEnumerable<TransitionDocNode> GetDisplayTransitions(TransitionGroupDocNode nodeGroup,
                                                                           DisplayTypeChrom displayType)
        {
            switch (displayType)
            {
                case DisplayTypeChrom.precursors:
                    // Return transitions that would be filtered from MS1
                    return nodeGroup.GetMsTransitions(true);
                case DisplayTypeChrom.products:
                    // Return transitions that would not be filtered in MS1
                    return nodeGroup.GetMsMsTransitions(true);
                default:
                    return nodeGroup.Transitions;
            }
        }

        public interface IStateProvider
        {
            TreeNodeMS SelectedNode { get; }
            IList<TreeNodeMS> SelectedNodes { get; }

            void SelectPath(IdentityPath path);

            SpectrumDisplayInfo SelectedSpectrum { get; }
            GraphValues.IRetentionTimeTransformOp GetRetentionTimeTransformOperation();

            void BuildChromatogramMenu(ZedGraphControl zedGraphControl, PaneKey paneKey, ContextMenuStrip menuStrip, ChromFileInfoId chromFileInfoId);
            PeptideGraphInfo GetPeptideGraphInfo(DocNode docNode);

            MsDataFileUri SelectedScanFile { get; }
            double SelectedScanRetentionTime { get; }
            Identity SelectedScanTransition { get; }
        }

        private const int FULLSCAN_TRACKING_INDEX = 0;
        private const int FULLSCAN_SELECTED_INDEX = 1;

        private string _nameChromatogramSet;
        private readonly IDocumentUIContainer _documentContainer;
        private readonly IStateProvider _stateProvider;
        private PointF _fullScanTrackingPointLocation;

        // Active graph state
        private readonly GraphHelper _graphHelper;
        private ChromExtractor? _extractor;
        private TransitionGroupDocNode[] _nodeGroups;
        private IdentityPath[] _groupPaths;
        private MeasuredResults _measuredResults;
        private ChromatogramGroupInfo[][] _arrayChromInfo;
        private bool _hasMergedChromInfo;
        private int _chromIndex;
        private bool _showPeptideTotals;
        private bool _enableTrackingDot;
        private bool _showingTrackingDot;

        private const int MaxPeptidesDisplayed = 100;
        private const int FullScanPointSize = 12;

        public GraphChromatogram(IStateProvider stateProvider, IDocumentUIContainer documentContainer, string name)
        {
            InitializeComponent();

            graphControl.GraphPane = new MSGraphPane();
            _graphHelper = GraphHelper.Attach(graphControl);
            NameSet = name;
            Icon = Resources.SkylineData;

            _nameChromatogramSet = name;
            _documentContainer = documentContainer;
            _documentContainer.ListenUI(OnDocumentUIChanged);
            _stateProvider = stateProvider;
            
            // Synchronize the zooming across all graph panes
            // Note that this only affects applying ZoomState to a graph pane.  Explicit changes 
            // to Scale Min/Max properties need to be manually applied to each axis.
            graphControl.IsSynchronizeXAxes = true;
        }

        public string NameSet
        {
            get { return _nameChromatogramSet; }
            set { TabText = _nameChromatogramSet = value; }
        }


        /// <summary>
        /// We have to limit the number of chromatogram windows to conserve window handles - so when
        /// a new one is desired, we simply update the contents of the oldest one. This preserves
        /// layout, and while it may result in an out of order display it's at least easy to understand.
        /// </summary>
        public void ChangeChromatogram(string name)
        {
            NameSet = name;
            _arrayChromInfo = null;
            _measuredResults = null;
            UpdateUI();
        }

        public int CurveCount { get { return GraphPanes.Sum(pane=>GetCurves(pane).Count()); } }

        private SrmDocument DocumentUI { get { return _documentContainer.DocumentUI; } }

        private IEnumerable<MSGraphPane> GraphPanes { get { return graphControl.MasterPane.PaneList.OfType<MSGraphPane>(); } }

        private static int LineWidth { get { return Settings.Default.ChromatogramLineWidth; } }
        private static float FontSize { get { return Settings.Default.ChromatogramFontSize; } }        

        private bool IsGroupActive { get { return _nodeGroups.SafeLength() > 0; } }
        private bool IsMultiGroup { get { return _nodeGroups.SafeLength() > 1; } }

        public IList<String> Files
        {
            get
            {
                IList<string> files = new List<string>();
                foreach (object file in comboFiles.Items)
                {
                    files.Add(file.ToString());
                }
                return files;
            }
        }

        public int? SelectedFileIndex
        {
            get
            {
                return comboFiles.SelectedItem != null ? comboFiles.SelectedIndex : (int?) null;
            }
            set
            {
                if (value.HasValue && value < comboFiles.Items.Count)
                {
                    comboFiles.SelectedIndex = value.Value;
                }
            }
        }

        [Browsable(true)]
        public event EventHandler<PickedPeakEventArgs> PickedPeak;

        /// <summary>
        /// Indicates a peak has been picked at a specified retention time
        /// for a specific replicate of a specific <see cref="TransitionGroupDocNode"/>.
        /// </summary>
        /// <param name="nodeGroup">The transition group for which the peak was picked</param>
        /// <param name="nodeTran">The transition no which the time was chosen</param>
        /// <param name="peakTime">The retention time at which the peak was picked</param>
        public void FirePickedPeak(TransitionGroupDocNode nodeGroup, TransitionDocNode nodeTran, ScaledRetentionTime peakTime)
        {
            if (PickedPeak != null)
            {
                var filePath = FilePath;
                if (filePath == null)
                    return;

                int iGroup = _nodeGroups.IndexOfReference(nodeGroup);
                var e = new PickedPeakEventArgs(_groupPaths[iGroup],
                                                nodeTran != null ? nodeTran.Id : null,
                                                _nameChromatogramSet,
                                                filePath,
                                                peakTime);
                PickedPeak(this, e);
            }
        }

        [Browsable(true)]
        public event EventHandler<ClickedChromatogramEventArgs> ClickedChromatogram;

        /// <summary>
        /// User clicked on a chromatogram.
        /// </summary>
        public void FireClickedChromatogram(GraphPane graphPane)
        {
            if (ClickedChromatogram == null)
                return;

            var clickedItem = (ChromGraphItem) _closestCurve.Tag;
            if (clickedItem.TransitionNode == null)
                return;
            var chromatogramInfo = clickedItem.Chromatogram;

            double displayTime = graphPane.CurveList[FULLSCAN_TRACKING_INDEX][0].X;
            var retentionTime = clickedItem.GetValidPeakBoundaryTime(displayTime);
            if (retentionTime.IsZero)
                return;
            IList<CurveItem> curveList;
            if (_nodeGroups.Length == 1)
            {
                // If there is only one precursor, then use the curves from all graph panes.
                curveList = graphControl.MasterPane.PaneList.SelectMany(GetCurveList).ToList();
            }
            else
            {
                curveList = GetCurveList(graphPane);
            }
            int scanIndex = MsDataFileScanHelper.FindScanIndex(chromatogramInfo, retentionTime.MeasuredTime);
            var transitions = new List<TransitionFullScanInfo>(curveList.Count);
            int? transitionIndex = null;
            foreach (var curve in curveList)
            {
                var graphItem = (ChromGraphItem) curve.Tag;
                if (ReferenceEquals(curve, _closestCurve))
                    transitionIndex = transitions.Count;
                var fullScanInfo = graphItem.FullScanInfo;
                transitions.Add(new TransitionFullScanInfo
                {
                    Name = fullScanInfo.ScanName,
                    Source = fullScanInfo.ChromInfo.Source,
                    TimeIntensities = fullScanInfo.ChromInfo.TimeIntensities,
                    Color = curve.Color,
                    PrecursorMz = fullScanInfo.ChromInfo.PrecursorMz,
                    ProductMz = fullScanInfo.ChromInfo.ProductMz,
                    ExtractionWidth = fullScanInfo.ChromInfo.ExtractionWidth,
                    _ionMobilityInfo = fullScanInfo.ChromInfo.GetIonMobilityFilter(),
                    Id = graphItem.TransitionNode.Id
                });
            }

            if (!transitionIndex.HasValue)
            {
                // Curve that they clicked on is no longer in CurveList
                return;
            }
            var measuredResults = DocumentUI.Settings.MeasuredResults;
            IScanProvider scanProvider = new ScanProvider(_documentContainer.DocumentFilePath, FilePath, 
                chromatogramInfo.Source, chromatogramInfo.Times, transitions.ToArray(), measuredResults);
            var e = new ClickedChromatogramEventArgs(
                scanProvider,
                transitionIndex.Value, 
                scanIndex);
            if (ClickedChromatogram != null)    // For ReSharper
                ClickedChromatogram(this, e);
        }

        [Browsable(true)]
        public event EventHandler<ChangedMultiPeakBoundsEventArgs> ChangedPeakBounds;

        public void SimulateChangedPeakBounds(List<ChangedPeakBoundsEventArgs> listChanges)
        {
            if (ChangedPeakBounds != null)
                ChangedPeakBounds(this, new ChangedMultiPeakBoundsEventArgs(listChanges.ToArray()));
        }

        /// <summary>
        /// Indicates a peak has been picked at a specified retention time
        /// for a specific replicate of a specific <see cref="TransitionGroupDocNode"/>.
        /// </summary>
        /// <param name="peakBoundDragInfos"></param>
        private void FireChangedPeakBounds(IEnumerable<PeakBoundsDragInfo> peakBoundDragInfos)
        {
            if (ChangedPeakBounds != null)
            {
                var filePath = FilePath;
                if (filePath == null)
                    return;

                var listChanges = new List<ChangedPeakBoundsEventArgs>();
                foreach (var dragInfo in peakBoundDragInfos)
                {
                    TransitionGroupDocNode nodeGroup = dragInfo.GraphItem.TransitionGroupNode;
                    TransitionDocNode nodeTran = null;
                    // If editing a single transition, then add the ID to the event
                    if (IsSingleTransitionDisplay && CurveCount == 1)
                        nodeTran = dragInfo.GraphItem.TransitionNode;

                    int iGroup =
                        _nodeGroups.IndexOf(node => ReferenceEquals(node.TransitionGroup, nodeGroup.TransitionGroup));
                    // If node no longer exists, give up
                    if (iGroup == -1)
                        return;
                    
                    var identified = dragInfo.IsIdentified
                                         ? (dragInfo.IsAlignedTimes
                                                ? PeakIdentification.ALIGNED
                                                : PeakIdentification.TRUE)
                                         : PeakIdentification.FALSE;

                    var e = new ChangedPeakBoundsEventArgs(_groupPaths[iGroup],
                                                           nodeTran != null ? nodeTran.Transition : null,
                                                           _nameChromatogramSet,
                                                           // All active groups should have the same file
                                                           filePath,
                                                           dragInfo.StartTime,
                                                           dragInfo.EndTime,
                                                           identified,
                                                           dragInfo.ChangeType);
                    listChanges.Add(e);
                }
                ChangedPeakBounds(this, new ChangedMultiPeakBoundsEventArgs(listChanges.ToArray()));
            }
        }

        [Browsable(true)]
        public event EventHandler<PickedSpectrumEventArgs> PickedSpectrum;

        public void FirePickedSpectrum(ScaledRetentionTime retentionTime)
        {
            if (PickedSpectrum != null)
                PickedSpectrum(this, new PickedSpectrumEventArgs(new SpectrumIdentifier(FilePath, retentionTime.MeasuredTime)));
        }

        [Browsable(true)]
        public event EventHandler<ZoomEventArgs> ZoomAll;

        private void graphControl_ZoomEvent(ZedGraphControl sender, ZoomState oldState, ZoomState newState, PointF mousePosition)
        {
            if (Settings.Default.AutoZoomAllChromatograms && ZoomAll != null)
                ZoomAll.Invoke(this, new ZoomEventArgs(newState));
        }

        /// <summary>
        /// Return the text of the annotations shown a chromatogram pane
        /// </summary>
        /// <param name="paneIndex">Index of the pane (default 0 or first)</param>
        /// <returns>Enumerable of annotation label text</returns>
        public IEnumerable<string> GetAnnotationLabelStrings(int paneIndex = 0)
        {
            return GraphPanes.ElementAt(paneIndex).GetAnnotationLabelStrings();
        }

        public void ZoomTo(ZoomState zoomState)
        {
            zoomState.ApplyState(GraphPanes.First());
        }

        /// <summary>
        /// Set min and max displayed time values, and optionally max intensity
        /// Note: this is intended for use in automated functional tests only
        /// </summary>
        public void ZoomTo(double rtStartMeasured, double rtEndMeasured, double? maxIntensity=null)
        {
            if (rtEndMeasured > rtStartMeasured)
            {                
                var pane = GraphPanes.First();
                pane.XAxis.Scale.Min = rtStartMeasured;
                pane.XAxis.Scale.Max = rtEndMeasured;
                pane.YAxis.Scale.Max = maxIntensity ?? pane.YAxis.Scale.Max;
                var hold = graphControl.IsSynchronizeXAxes;
                graphControl.IsSynchronizeXAxes = false; // just this one
                ZoomState.ApplyState(pane);
                if (ZoomAll != null)
                    ZoomAll.Invoke(this, new ZoomEventArgs(ZoomState));
                graphControl.IsSynchronizeXAxes = hold;
            }
        }

        public void ZoomToPeak(double rtStart, double rtEnd)
        {
            _graphHelper.ZoomToPeak(rtStart, rtEnd);
            graphControl.AxisChange();
            using (var graphics = graphControl.CreateGraphics())
            {
                foreach (var graphPane in graphControl.MasterPane.PaneList.OfType<MSGraphPane>())
                {
                    graphPane.SetScale(graphics);
                }
            }
            graphControl.Invalidate();
        }

        public ZoomState ZoomState
        {
            get { return new ZoomState(GraphPanes.First(), ZoomState.StateType.Zoom); }
        }

        public void LockZoom()
        {
            _graphHelper.LockZoom();
        }

        public void UnlockZoom()
        {
            _graphHelper.UnlockZoom();
        }

        public PointF TransformCoordinates(double x, double y, PaneKey? paneKey, CoordType coordType = CoordType.AxisXYScale)
        {
            var graphPane = _graphHelper.GetGraphPane(paneKey ?? PaneKey.DEFAULT);
            return graphPane.GeneralTransform(new PointF((float)x, (float)y), coordType);
        }

        public void OnDocumentUIChanged(object sender, DocumentChangedEventArgs e)
        {
            // Changes to the settings are handled elsewhere
            if (e.DocumentPrevious != null &&
                ReferenceEquals(DocumentUI.Settings.MeasuredResults,
                                e.DocumentPrevious.Settings.MeasuredResults))
            {
                // Update the graph if it is no longer current, due to changes
                // within the document node tree.
                if (Visible && !IsDisposed && !IsCurrent(e.DocumentPrevious != null
                                                             ? e.DocumentPrevious.Settings
                                                             : null,
                                                         DocumentUI.Settings))
                {
                    UpdateUI();
                }
            }
        }

        public bool IsCacheInvalidated { get; set; }

        public bool IsCurrent(SrmSettings settingsOld, SrmSettings settingsNew)
        {
            if (IsCacheInvalidated)
                return false;

            // Changing integration all setting invalidates the graph
            if (settingsOld != null && settingsOld.TransitionSettings.Integration.IsIntegrateAll !=
                                       settingsNew.TransitionSettings.Integration.IsIntegrateAll)
                return false;

            // if the ChromatogramSet has changed, then we might have gone from
            // "Chromatogram Information Unavailable" to being available.
            if (settingsOld != null && settingsOld.HasResults && settingsNew.HasResults)
            {
                ChromatogramSet chromatogramSetOld;
                int resultsIndexOld;
                ChromatogramSet chromatogramSetNew;
                int resultsIndexNew;
                if (settingsOld.MeasuredResults.TryGetChromatogramSet(_nameChromatogramSet, out chromatogramSetOld, out resultsIndexOld)
                    != settingsNew.MeasuredResults.TryGetChromatogramSet(_nameChromatogramSet, out chromatogramSetNew, out resultsIndexNew))
                {
                    return false;
                }
                if (!ReferenceEquals(chromatogramSetOld, chromatogramSetNew))
                {
                    return false;
                }
            }

            // Check if any of the charted transition groups have changed
            if (_nodeGroups == null)
                return true;

            for (int i = 0; i < _nodeGroups.Length; i++)
            {
                var nodeGroup = _nodeGroups[i];
                var nodeGroupCurrent = (TransitionGroupDocNode)
                                        _documentContainer.DocumentUI.FindNode(_groupPaths[i]);
                if (!ReferenceEquals(nodeGroup, nodeGroupCurrent))
                {
                    // Make sure the actual results for this graph have changed
                    if (nodeGroup == null || nodeGroupCurrent == null ||
                        nodeGroup.Results == null || nodeGroupCurrent.Results == null ||
                        nodeGroup.Children.Count != nodeGroupCurrent.Children.Count)
                        return false;

                    // Protect against _chromIndex == -1, reported as an unexpected error
                    if (_chromIndex < 0)
                        continue;

                    // Need to compare the transition results, because it is possible
                    // for a transition result to change in a way that effects the charts
                    // without changing the group.
                    for (int j = 0, len = nodeGroup.Children.Count; j < len; j++)
                    {
                        var nodeTran = (TransitionDocNode) nodeGroup.Children[j];
                        var nodeTranCurrent = (TransitionDocNode) nodeGroupCurrent.Children[j];
                        if (nodeTran.Results.Count <= _chromIndex ||
                            nodeTranCurrent.Results.Count <= _chromIndex ||
                            !Equals(nodeTran.Results[_chromIndex], nodeTranCurrent.Results[_chromIndex]))
                            return false;
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// Gets the set of chomatogram info for the selected file of the groups.
        /// </summary>
        public ChromatogramGroupInfo[] ChromGroupInfos
        {
            get
            {
                int iSelected = 0;
                if (toolBar.Visible)
                    iSelected = comboFiles.SelectedIndex;

                return _arrayChromInfo?[iSelected];
            }
        }

        public ChromFileInfoId GetChromFileInfoId()
        {
            var document = DocumentUI;
            if (!document.Settings.HasResults || null == _arrayChromInfo)
            {
                return null;
            }
            ChromatogramSet chromatograms;
            if (!document.Settings.MeasuredResults.TryGetChromatogramSet(_nameChromatogramSet, out chromatograms, out _chromIndex))
            {
                return null;
            }
            var chromGroupInfo = ChromGroupInfos[0];
            if (chromGroupInfo == null)
            {
                return null;
            }
            return chromatograms.FindFile(chromGroupInfo);
        }

        public double? SelectedRetentionTimeMsMs
        {
            get { return RTGraphItem.SelectedRetentionMsMs; }
        }

        public double? PredictedRT
        {
            get { return RTGraphItem.RetentionPrediction; }
        }

        private ChromGraphItem RTGraphItem
        {
            get { return GetGraphItems(graphControl.GraphPane).Last(); }
        }
        public GraphPane GraphPane
        {
            get { return graphControl.GraphPane; }
        }

        public MSGraphControl GraphControl
        {
            get { return graphControl; }
        }

        public double? BestPeakTime
        {
            get
            {
                var graphItem = GetGraphItems(graphControl.GraphPane).First(g => g.BestPeakTime > 0);
                return graphItem.BestPeakTime;
            }
        }

        /// <summary>
        /// Returns the file path for the selected file of the groups.
        /// </summary>
        public MsDataFileUri FilePath
        {
            get
            {
                return ChromGroupInfo?.FilePath;
            }
        }

        private ChromatogramGroupInfo ChromGroupInfo
        {
            get
            {
                // CONSIDER: Is this really the selected file?
                return ChromGroupInfos.FirstOrDefault(info => info != null);
            }
        }

        private LockMassParameters LockMassParameters
        {
            get
            {
                var filePath = FilePath;
                if (filePath == null)
                    return null;
                return filePath.GetLockMassParameters();
            }
        }

        private bool _dontSyncSelectedFile;

        private void comboFiles_SelectedIndexChanged(object sender, EventArgs e)
        {
            // If the selected file is changing, then all of the chromatogram data
            // on display will change, and the graph should be auto-zoomed, if
            // any auto-zooming is turned on.
            UpdateUI();

            if (_dontSyncSelectedFile)
                return;

            var panes = FormUtil.OpenForms.OfType<GraphSummary>().SelectMany(g => g.GraphPanes).OfType<SummaryReplicateGraphPane>();

            foreach (var pane in panes)
            {  
                var item = comboFiles.SelectedItem.ToString();
                pane.SetSelectedFile(item);
            }
        }

        private void GraphChromatogram_VisibleChanged(object sender, EventArgs e)
        {
            UpdateUI();
        }

        private void GraphChromatogram_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Escape:
                    if (_peakBoundDragInfos != null)
                        EndDrag(false);
                    else
                        _documentContainer.FocusDocument();
                    break;
            }
        }

        public void UpdateUI(bool selectionChanged = true)
        {            
            IsCacheInvalidated = false;

            // Only worry about updates, if the graph is visible
            // And make sure it is not disposed, since rendering happens on a timer
            if (!Visible || IsDisposed)
                return;

            GraphHelper.FormatGraphPane(graphControl.GraphPane);
            GraphHelper.FormatFontSize(graphControl.GraphPane,Settings.Default.ChromatogramFontSize);
            var settings = DocumentUI.Settings;
            var results = settings.MeasuredResults;
            if (results == null)
                return;
            ChromatogramSet chromatograms;
            if (!results.TryGetChromatogramSet(_nameChromatogramSet, out chromatograms, out _chromIndex))
                return;

            string xAxisTitle = GraphValues.ToLocalizedString(RTPeptideValue.Retention);
            RegressionLine timeRegressionFunction = null;
            var retentionTimeTransformOp = _stateProvider.GetRetentionTimeTransformOperation();
            if (null != retentionTimeTransformOp && null != _arrayChromInfo)
            {
                Assume.IsNotNull(chromatograms, @"chromatograms");
                Assume.IsNotNull(ChromGroupInfos, @"ChromGroupInfos");
                if (ChromGroupInfos != null && ChromGroupInfos.Length > 0 && null != ChromGroupInfos[0])
                {
                    retentionTimeTransformOp.TryGetRegressionFunction(chromatograms.FindFile(ChromGroupInfos[0]), out timeRegressionFunction);
                }
                if (null != timeRegressionFunction)
                {
                    xAxisTitle = retentionTimeTransformOp.GetAxisTitle(RTPeptideValue.Retention);
                }
            }

            // Try to find a tree node with spectral library info associated
            // with the current selection.
            var nodeTree = _stateProvider.SelectedNode as SrmTreeNode;
            var nodeGroupTree = nodeTree as TransitionGroupTreeNode;
            var nodeTranTree = nodeTree as TransitionTreeNode;
            if (nodeTranTree != null)
                nodeGroupTree = nodeTranTree.Parent as TransitionGroupTreeNode;

            PeptideDocNode[] nodePeps = null;
            Target lookupSequence = null;
            ExplicitMods lookupMods = null;
            TransitionGroupDocNode[] nodeGroups = null;
            IdentityPath[] groupPaths = null;
            PeptideTreeNode nodePepTree;
            var peptideAndTransitionGroups = GetSelectedPeptides();
            if (peptideAndTransitionGroups.ShowPeptideTotals)
            {
                // Display transition totals for multiple peptides.
                nodePeps = peptideAndTransitionGroups.NodePeps.ToArray();
                nodeGroups = peptideAndTransitionGroups.NodeGroups.ToArray();
                groupPaths = peptideAndTransitionGroups.GroupPaths.ToArray();
            }
            else if (nodeGroupTree != null)
            {
                nodePepTree = nodeGroupTree.Parent as PeptideTreeNode;
                if (nodePepTree != null)
                {
                    nodePeps = new[] {nodePepTree.DocNode};
                    lookupSequence = nodePepTree.DocNode.SourceUnmodifiedTarget;
                    lookupMods = nodePepTree.DocNode.SourceExplicitMods;
                }
                nodeGroups = new[] {nodeGroupTree.DocNode};
                groupPaths = new[] {nodeGroupTree.Path};
            }
            else
            {
                nodePepTree = nodeTree as PeptideTreeNode;
                if (nodePepTree != null && nodePepTree.ChildDocNodes.Count > 0)
                {
                    var children = nodePepTree.ChildDocNodes;
                    nodePeps = new PeptideDocNode[children.Count];
                    nodeGroups = new TransitionGroupDocNode[children.Count];
                    groupPaths = new IdentityPath[children.Count];
                    var pathParent = nodePepTree.Path;
                    for (int i = 0; i < nodeGroups.Length; i++)
                    {
                        var nodeGroup = (TransitionGroupDocNode) children[i];
                        nodePeps[i] = nodePepTree.DocNode;
                        nodeGroups[i] = nodeGroup;
                        groupPaths[i] = new IdentityPath(pathParent, nodeGroup.Id);
                    }
                    lookupSequence = nodePepTree.DocNode.SourceUnmodifiedTarget;
                    lookupMods = nodePepTree.DocNode.SourceExplicitMods;
                }
            }

            // Clear existing data from the graph pane
            _graphHelper.ResetForChromatograms(
                nodeGroups == null ? null : nodeGroups.Select(node=>node.TransitionGroup),
                peptideAndTransitionGroups.ProteinSelected);

            RetentionTimeValues firstBestPeak = null;
            RetentionTimeValues lastBestPeak = null;

            // Check for appropriate chromatograms to load
            bool changedGroups = false;

            var displayToExtractor = new Dictionary<DisplayTypeChrom, ChromExtractor>
            {
                {DisplayTypeChrom.tic, ChromExtractor.summed},
                {DisplayTypeChrom.base_peak, ChromExtractor.base_peak},
                {DisplayTypeChrom.qc, ChromExtractor.qc}
            };

            try
            {
                _showPeptideTotals = peptideAndTransitionGroups.ShowPeptideTotals;
                _enableTrackingDot = false;

                // Make sure all the chromatogram info for the relevant transition groups is present.
                float mzMatchTolerance = (float) settings.TransitionSettings.Instrument.MzMatchTolerance;
                var displayType = GetDisplayType(DocumentUI);
                bool changedGroupIds;
                if (displayToExtractor.ContainsKey(displayType))
                {
                    var extractor = displayToExtractor[displayType];
                    if (EnsureChromInfo(results,
                                        chromatograms,
                                        nodeGroups,
                                        groupPaths,
                                        extractor,
                                        out changedGroups,
                                        out changedGroupIds))
                    {
                        // Update the file choice toolbar, if the set of groups has changed
                        // Overkill for summary graphs, but the files still might change with any
                        // group change
                        if (changedGroups)
                        {
                            UpdateToolbar(_arrayChromInfo);
                            EndDrag(false);
                        }

                        var nodeTranSelected = (nodeTranTree != null ? nodeTranTree.DocNode : null);
                        DisplayAllIonsSummary(timeRegressionFunction, nodeTranSelected, chromatograms, extractor,
                            out RetentionTimeValues bestPeakTimes);
                        firstBestPeak = lastBestPeak = bestPeakTimes;

                        if (nodeGroups != null && lookupSequence != null)
                        {
                            foreach (var chromGraphItem in _graphHelper.ListPrimaryGraphItems())
                            {
                                SetRetentionTimeIdIndicators(chromGraphItem.Value, settings, nodeGroups,
                                    lookupSequence, lookupMods);
                            }
                        }
                    }
                }
                else if (peptideAndTransitionGroups.ShowPeptideTotals)
                {
                    if (EnsureChromInfo(results,
                                        chromatograms,
                                        nodePeps,
                                        nodeGroups,
                                        groupPaths,
                                        mzMatchTolerance,
                                        out changedGroups,
                                        out changedGroupIds))
                    {
                        // Update the file choice toolbar, if the set of groups has changed
                        if (changedGroups)
                        {
                            UpdateToolbar(_arrayChromInfo);
                            EndDrag(false);
                        }

                        int countLabelTypes = settings.PeptideSettings.Modifications.CountLabelTypes;
                        DisplayPeptides(timeRegressionFunction, chromatograms, mzMatchTolerance,
                            countLabelTypes, nodePeps, out firstBestPeak, out lastBestPeak);
                        foreach (var msGraphPane in GraphPanes)
                        {
                            msGraphPane.Legend.IsVisible = false;
                            msGraphPane.AllowLabelOverlap = false;
                        }
                    }
                }
                else if (nodeGroups != null && EnsureChromInfo(results,
                                                               chromatograms,
                                                               nodePeps,
                                                               nodeGroups,
                                                               groupPaths,
                                                               mzMatchTolerance,
                                                               out changedGroups,
                                                               out changedGroupIds))
                {
                    // Update the file choice toolbar, if the set of groups has changed
                    if (changedGroups)
                    {
                        UpdateToolbar(_arrayChromInfo);
                        EndDrag(false);
                    }

                    bool multipleGroupsPerPane;
                    bool nodeGroupsInSeparatePanes = false;
                    if (_graphHelper.AllowSplitGraph)
                    {
                        var nodeGroupGraphPaneKeys = nodeGroups.Select(nodeGroup => new PaneKey(nodeGroup)).ToArray();
                        // ReSharper disable PossibleMultipleEnumeration
                        var countDistinctGraphPaneKeys = nodeGroupGraphPaneKeys.Distinct().Count();
                        multipleGroupsPerPane = countDistinctGraphPaneKeys != nodeGroupGraphPaneKeys.Length;
                        // ReSharper restore PossibleMultipleEnumeration
                        nodeGroupsInSeparatePanes = countDistinctGraphPaneKeys > 1;
                    }
                    else
                    {
                        multipleGroupsPerPane = nodeGroups.Length > 1;
                    }

                    // If displaying multiple groups or the total of a single group
                    if (multipleGroupsPerPane || DisplayType == DisplayTypeChrom.total)
                    {
                        int countLabelTypes = settings.PeptideSettings.Modifications.CountLabelTypes;
                        DisplayTotals(timeRegressionFunction, chromatograms, mzMatchTolerance, 
                                      countLabelTypes, out RetentionTimeValues bestPeakTimes);
                        firstBestPeak = lastBestPeak = bestPeakTimes;
                    }
                        // Single group with optimization data, not a transition selected,
                        // and single display mode
                    else if (chromatograms.OptimizationFunction != null &&
                             nodeTranTree == null && IsSingleTransitionDisplay)
                    {
                        DisplayOptimizationTotals(timeRegressionFunction, chromatograms, mzMatchTolerance,
                                                  out RetentionTimeValues bestPeakTimes);
                        firstBestPeak = lastBestPeak = bestPeakTimes;
                    }
                    else
                    {
                        var nodeTranSelected = (nodeTranTree != null ? nodeTranTree.DocNode : null);
                        bool enableTrackingDot = false;
                        RetentionTimeValues bestQuantitativePeak = null;
                        RetentionTimeValues bestNonQuantitativePeak = null;
                        for (int i = 0; i < _nodeGroups.Length; i++)
                        {
                            var nodeGroup = _nodeGroups[i];
                            var chromGroupInfo = ChromGroupInfos[i];
                            if (chromGroupInfo == null)
                                continue;
                            if (!_graphHelper.AllowSplitGraph || nodeGroupsInSeparatePanes)
                            {
                                DisplayTransitions(timeRegressionFunction, nodeTranSelected, chromatograms,
                                                   mzMatchTolerance,
                                                   nodeGroup, chromGroupInfo,
                                                   new PaneKey(nodeGroup),
                                                   GetDisplayType(DocumentUI, nodeGroup), ref bestQuantitativePeak, ref bestNonQuantitativePeak);
                                enableTrackingDot = enableTrackingDot || _enableTrackingDot;
                            }
                            else
                            {
                                displayType = GetDisplayType(DocumentUI, nodeGroup);
                                if (displayType != DisplayTypeChrom.products)
                                {
                                    DisplayTransitions(timeRegressionFunction, nodeTranSelected, chromatograms, mzMatchTolerance,
                                                       nodeGroup, chromGroupInfo, PaneKey.PRECURSORS, DisplayTypeChrom.precursors,
                                                       ref bestQuantitativePeak, ref bestNonQuantitativePeak);
                                    enableTrackingDot = enableTrackingDot || _enableTrackingDot;
                                }
                                if (displayType != DisplayTypeChrom.precursors)
                                {
                                    DisplayTransitions(timeRegressionFunction, nodeTranSelected, chromatograms, mzMatchTolerance, 
                                                       nodeGroup, chromGroupInfo, PaneKey.PRODUCTS, DisplayTypeChrom.products,
                                                       ref bestQuantitativePeak, ref bestNonQuantitativePeak);
                                    enableTrackingDot = enableTrackingDot || _enableTrackingDot;
                                }
                            }
                        }

                        firstBestPeak = lastBestPeak = bestQuantitativePeak ?? bestNonQuantitativePeak;
                        _enableTrackingDot = enableTrackingDot;

                        // Should we show the scan selection point?
                        if (_arrayChromInfo != null && Equals(_stateProvider.SelectedScanFile, FilePath) && _stateProvider.SelectedScanTransition != null)
                        {
                            foreach (var graphPane in GraphPanes)
                            {
                                var transitionCurve = GetTransitionCurve(graphPane);
                                if (transitionCurve == null)
                                    continue;
                                var graphItem = transitionCurve.Tag as ChromGraphItem;
                                if (graphItem == null)
                                    continue;
                                int rtIndex = graphItem.GetNearestMeasuredIndex(_stateProvider.SelectedScanRetentionTime);
                                if (rtIndex == -1)
                                    continue;
                                // for each curve add the measured points
                                var lineItem = (LineItem) graphPane.CurveList[FULLSCAN_SELECTED_INDEX];
                                lineItem[0].X = transitionCurve.Points[rtIndex].X;
                                lineItem[0].Y = transitionCurve.Points[rtIndex].Y;
                                lineItem.Symbol.Fill.Color = Color.FromArgb(150, transitionCurve.Color);
                                lineItem.IsVisible = true;
                                break;
                            }
                        }
                    }

                    foreach (var chromGraphItem in _graphHelper.ListPrimaryGraphItems())
                    {
                        SetRetentionTimeIndicators(chromGraphItem.Value, settings, chromatograms, nodePeps, nodeGroups,
                            lookupSequence, lookupMods);
                    }
                }
            }
            catch (InvalidDataException x)
            {
                DisplayFailureGraph(nodeGroups, x);
            }
            catch (IOException x)
            {
                DisplayFailureGraph(nodeGroups, x);
            }
            // Error decompressing chromatograms
            catch (NotSupportedException x)
            {
                DisplayFailureGraph(nodeGroups, x);
            }
            // Skyd file on a network drive where access is restricted
            catch (UnauthorizedAccessException x)
            {
                DisplayFailureGraph(nodeGroups, x);
            }
            // Can happen in race condition where file is released before UI cleaned up
            catch (ObjectDisposedException x)
            {
                DisplayFailureGraph(nodeGroups, x);
            }

            // Show unavailable message, if no chromatogram loaded
            if (peptideAndTransitionGroups.ShowPeptideTotals)
            {
                _graphHelper.FinishedAddingChromatograms(new []{firstBestPeak, lastBestPeak}, true);
            }
            else if (!_graphHelper.ListPrimaryGraphItems().Any())
            {
                if (nodeGroups == null || changedGroups)
                {
                    UpdateToolbar(null);
                    EndDrag(false);
                }
                if (CurveCount == 0)
                {
                    string message = null;
                    if (nodePeps == null)
                        message = Resources.GraphChromatogram_UpdateUI_Select_a_peptide__precursor_or_transition_to_view_its_chromatograms;
                    else switch (DisplayType)
                    {
                        case DisplayTypeChrom.precursors:
                            message = Resources.GraphChromatogram_UpdateUI_No_precursor_ion_chromatograms_found;
                            break;
                        case DisplayTypeChrom.products:
                            message = Resources.GraphChromatogram_UpdateUI_No_product_ion_chromatograms_found;
                            break;
                        case DisplayTypeChrom.base_peak:
                            message = Resources.GraphChromatogram_UpdateUI_No_base_peak_chromatogram_found;
                            break;
                        case DisplayTypeChrom.tic:
                            message = Resources.GraphChromatogram_UpdateUI_No_TIC_chromatogram_found;
                            break;
                        case DisplayTypeChrom.qc:
                            message = Resources.GraphChromatogram_UpdateUI_No_QC_chromatogram_found;
                            break;
                    }
                    SetGraphItem(new UnavailableChromGraphItem(Helpers.PeptideToMoleculeTextMapper.Translate(message, DocumentUI.DocumentType)));
                }
            }
            else
            {
                _graphHelper.FinishedAddingChromatograms(new[] {firstBestPeak, lastBestPeak}, false);
            }

            foreach (var graphPane in GraphPanes)
            {
                graphPane.XAxis.Title.Text = xAxisTitle;

                GraphHelper.FormatGraphPane(graphPane);
                GraphHelper.FormatFontSize(graphPane, Settings.Default.ChromatogramFontSize);
            }
 
            graphControl.IsEnableVPan = graphControl.IsEnableVZoom = !Settings.Default.LockYChrom;

            Refresh();
        }

        private CurveItem GetTransitionCurve(GraphPane graphPane)
        {
            foreach (var curve in GetCurves(graphPane))
            {
                var graphItem = curve.Tag as ChromGraphItem;
                if (graphItem != null && graphItem.TransitionNode != null &&
                    ReferenceEquals(_stateProvider.SelectedScanTransition, graphItem.TransitionNode.Id))
                {
                    return curve;
                }
            }
            return null;
        }

        private GraphPane GetScanSelectedPane()
        {
            if (_stateProvider.SelectedScanTransition != null)
            {
                foreach (var graphPane in graphControl.MasterPane.PaneList)
                {
                    if (GetTransitionCurve(graphPane) != null)
                        return graphPane;
                }
            }

            return null;
        }

        private PeptidesAndTransitionGroups GetSelectedPeptides()
        {
            return PeptidesAndTransitionGroups.Get(_stateProvider.SelectedNodes, _chromIndex, MaxPeptidesDisplayed);
        }

        private void DisplayFailureGraph(IEnumerable<TransitionGroupDocNode> nodeGroups,
                                         Exception x)
        {
            if (nodeGroups != null)
            {
                var errorItems = new List<IMSGraphItemInfo>();
                foreach (var nodeGroup in nodeGroups)
                {
                    errorItems.Add(new FailedChromGraphItem(nodeGroup, x));
                }
                _graphHelper.SetErrorGraphItems(errorItems);
            }
        }

        private void DisplayAllIonsSummary(RegressionLine timeRegressionFunction,
                                           TransitionDocNode nodeTranSelected,
                                           ChromatogramSet chromatograms,
                                           ChromExtractor extractor,
                                           out RetentionTimeValues bestRetentionTimes)
        {
            bestRetentionTimes = null;
            if (ChromGroupInfos.Length == 0)
            {
                return;
            }
            var chromGroupInfo = ChromGroupInfos[0];
            var fileId = chromatograms.FindFile(chromGroupInfo);

            var nodeGroup = _nodeGroups != null ? _nodeGroups.FirstOrDefault() : null;
            if (nodeGroup == null)
                nodeTranSelected = null;
            var info = chromGroupInfo.GetTransitionInfo(null, 0, TransformChrom.raw, chromatograms.OptimizationFunction);

            TransitionChromInfo tranPeakInfo = null;
            RetentionTimeValues bestQuantitativePeakTimes = null;
            RetentionTimeValues bestNonQuantitativePeakTimes = null;
            if (nodeGroup != null)
            {
                float maxPeakHeight = float.MinValue;
                foreach (TransitionDocNode nodeTran in nodeGroup.Children)
                {
                    // Keep track of which chromatogram owns the tallest member of
                    // the peak on the document tree.
                    var transitionChromInfo = GetTransitionChromInfo(nodeTran, _chromIndex, fileId, 0);
                    if (transitionChromInfo == null)
                        continue;

                    if (nodeTranSelected != null)
                    {
                        if (ReferenceEquals(nodeTran, nodeTranSelected))
                            tranPeakInfo = transitionChromInfo;
                    }
                    else if (transitionChromInfo.Height > maxPeakHeight)
                    {
                        maxPeakHeight = transitionChromInfo.Height;
                        tranPeakInfo = transitionChromInfo;
                    }

                    var retentionTimeValues = RetentionTimeValues.FromTransitionChromInfo(transitionChromInfo);
                    if (IsQuantitative(nodeTran))
                    {
                        bestQuantitativePeakTimes = RetentionTimeValues.Merge(bestQuantitativePeakTimes, retentionTimeValues);
                    }
                    else
                    {
                        bestNonQuantitativePeakTimes = RetentionTimeValues.Merge(bestNonQuantitativePeakTimes, retentionTimeValues);
                    }
                }
            }

            bestRetentionTimes = bestQuantitativePeakTimes ?? bestNonQuantitativePeakTimes;

            // Apply active transform
            info.Transform(Transform);

            int numPeaks = info.NumPeaks;
            var annotationFlags = new bool[numPeaks];
            for (int i = 0; i < numPeaks; i++)
            {
                // Exclude any peaks between the boundaries of the chosen peak.
                annotationFlags[i] = !IntersectPeaks(info.GetPeak(i), tranPeakInfo);
            }
            var graphItem = new ChromGraphItem(nodeGroup,
                                               nodeTranSelected,
                                               info,
                                               tranPeakInfo,
                                               timeRegressionFunction,
                                               annotationFlags,
                                               null,
                                               0,
                                               true,
                                               true,
                                               null,
                                               0,
                                               COLORS_GROUPS[(int)extractor % COLORS_GROUPS.Count],
                                               FontSize,
                                               LineWidth);
            _graphHelper.AddChromatogram(new PaneKey(nodeGroup), graphItem);
        }

        private void SetGraphItem(IMSGraphItemInfo graphItem)
        {
            _graphHelper.ResetForChromatograms(null);
            _graphHelper.SetErrorGraphItem(graphItem);
        }

        private void DisplayTransitions(RegressionLine timeRegressionFunction,
                                        TransitionDocNode nodeTranSelected,
                                        ChromatogramSet chromatograms,
                                        float mzMatchTolerance,
                                        TransitionGroupDocNode nodeGroup,
                                        ChromatogramGroupInfo chromGroupInfo,
                                        PaneKey graphPaneKey,
                                        DisplayTypeChrom displayType,
                                        ref RetentionTimeValues bestQuantitativePeak,
                                        ref RetentionTimeValues bestNonQuantitativePeak)
        {
            var fileId = chromatograms.FindFile(chromGroupInfo);

            // Get points for all transitions, and pick maximum peaks.
            ChromatogramInfo[] arrayChromInfo;
            var displayTrans = GetDisplayTransitions(nodeGroup, displayType).ToArray();
            int numTrans = displayTrans.Length;
            int numSteps = 0;
            bool allowEmpty = false;
            
            if (IsSingleTransitionDisplay && nodeTranSelected != null)
            {
                if (!displayTrans.Contains(nodeTranSelected))
                {
                    arrayChromInfo = new ChromatogramInfo[0];
                    displayTrans = new TransitionDocNode[0];
                    numTrans = 0;
                }
                else
                {
                    var listChromInfo = chromGroupInfo.GetAllTransitionInfo(nodeTranSelected,
                        mzMatchTolerance, chromatograms.OptimizationFunction, TransformChrom.raw);
                    numSteps = listChromInfo.StepCount;
                    numTrans = numSteps * 2 + 1;
                    displayTrans = Enumerable.Repeat(nodeTranSelected, numTrans).ToArray();
                    arrayChromInfo = Enumerable.Range(-numSteps, numTrans)
                        .Select(step => listChromInfo.GetChromatogramForStep(step)).ToArray();
                    allowEmpty = true;
                }
            }
            else
            {
                arrayChromInfo = new ChromatogramInfo[numTrans];
                for (int i = 0; i < numTrans; i++)
                {
                    var nodeTran = displayTrans[i];
                    // Get chromatogram info for this transition
                    arrayChromInfo[i] = chromGroupInfo.GetTransitionInfo(nodeTran, mzMatchTolerance, TransformChrom.raw, chromatograms.OptimizationFunction);
                }
            }

            bool anyQuantitative = displayTrans.Any(IsQuantitative);
            int bestPeakTran = -1;
            TransitionChromInfo tranPeakInfo = null;
            float maxPeakHeight = float.MinValue;
            int numPeaks = chromGroupInfo.NumPeaks;
            var maxPeakTrans = new int[numPeaks];
            var maxPeakHeights = new float[numPeaks];
            for (int i = 0; i < numPeaks; i++)
                maxPeakHeights[i] = float.MinValue;
            var transform = Transform;
            // Prepare arrays of values for library dot-product
            double[] expectedIntensities = null;
            double[][] peakAreas = null;
            bool isShowingMs = displayTrans.Any(nodeTran => nodeTran.IsMs1);
            bool isShowingMsMs = displayTrans.Any(nodeTran => !nodeTran.IsMs1);
            bool isFullScanMs = DocumentUI.Settings.TransitionSettings.FullScan.IsEnabledMs && isShowingMs;
            if ((isFullScanMs && !isShowingMsMs && nodeGroup.HasIsotopeDist) ||
                (!isFullScanMs && nodeGroup.HasLibInfo))
            {
                expectedIntensities = new double[numTrans];
                peakAreas = new double[numPeaks][];
                for (int i = 0; i < numPeaks; i++)
                    peakAreas[i] = new double[numTrans];
            }

            // Find the transition with the maximum peak height for the best peak
            for (int i = 0; i < numTrans; i++)
            {
                var nodeTran = displayTrans[i];
                int step = (numSteps > 0 ? i - numSteps : 0);
                var transitionChromInfo = GetTransitionChromInfo(nodeTran, _chromIndex, fileId, step);
                if (transitionChromInfo == null)
                    continue;
                bool quantitative = IsQuantitative(nodeTran);
                if (quantitative || !anyQuantitative)
                {
                    if (maxPeakHeight < transitionChromInfo.Height)
                    {
                        maxPeakHeight = transitionChromInfo.Height;
                        bestPeakTran = i;
                        tranPeakInfo = transitionChromInfo;
                    }
                }

                if (quantitative)
                {
                    bestQuantitativePeak = RetentionTimeValues.Merge(bestQuantitativePeak, RetentionTimeValues.FromTransitionChromInfo(transitionChromInfo));
                }
                else
                {
                    bestNonQuantitativePeak = RetentionTimeValues.Merge(bestNonQuantitativePeak, RetentionTimeValues.FromTransitionChromInfo(transitionChromInfo));
                }
            }

            for (int i = 0; i < numTrans; i++)
            {
                var nodeTran = displayTrans[i];
                if (IsQuantitative(nodeTran))
                {

                    // Store library intensities for dot-product
                    if (expectedIntensities != null)
                    {
                        if (isFullScanMs)
                            expectedIntensities[i] = nodeTran.HasDistInfo ? nodeTran.IsotopeDistInfo.Proportion : 0;
                        else
                            expectedIntensities[i] = nodeTran.HasLibInfo ? nodeTran.LibInfo.Intensity : 0;
                    }
                }

                var info = arrayChromInfo[i];
                if (info == null)
                    continue;

                // Apply any active transform
                info.Transform(transform);
                if (!IsQuantitative(nodeTran))
                {
                    continue;
                }

                for (int j = 0; j < numPeaks; j++)
                {
                    var peak = info.GetPeak(j);

                    // Exclude any peaks between the boundaries of the chosen peak.
                    if (IntersectPeaks(peak, tranPeakInfo))
                        continue;

                    // Store peak intensity for dot-product
                    if (peakAreas != null)
                        peakAreas[j][i] = peak.Area;

                    // Keep track of which transition has the max height for each peak
                    if (maxPeakHeights[j] < peak.Height)
                    {
                        maxPeakHeights[j] = peak.Height;
                        maxPeakTrans[j] = i;
                    }
                }
            }

            // Calculate library dot-products, if possible
            double[] dotProducts = null;
            double bestProduct = 0;
            int minProductTrans = isFullScanMs
                                        ? TransitionGroupDocNode.MIN_DOT_PRODUCT_MS1_TRANSITIONS
                                        : TransitionGroupDocNode.MIN_DOT_PRODUCT_TRANSITIONS;
            if (peakAreas != null && numTrans >= minProductTrans)
            {
                var tranGroupChromInfo = GetTransitionGroupChromInfo(nodeGroup, fileId, _chromIndex);
                double? dotProduct = null;
                if (tranGroupChromInfo != null)
                {
                    dotProduct = isFullScanMs
                                        ? tranGroupChromInfo.IsotopeDotProduct
                                        : tranGroupChromInfo.LibraryDotProduct;
                }
                if (dotProduct.HasValue)
                {
                    bestProduct = dotProduct.Value;

                    var statExpectedIntensities = new Statistics(expectedIntensities);
                    for (int i = 0; i < peakAreas.Length; i++)
                    {
                        var statPeakAreas = new Statistics(peakAreas[i]);
                        double dotProductCurrent = statPeakAreas.NormalizedContrastAngleSqrt(statExpectedIntensities);
                        // Only show products that are greater than the best peak product,
                        // and by enough to be a significant improvement.  Also the library product
                        // on the group node is stored as a float, which means the version
                        // hear calculated as a double can be larger, but really represent
                        // the same number.
                        if (dotProductCurrent > bestProduct &&
                            dotProductCurrent > 0.5 &&
                            dotProductCurrent - bestProduct > 0.05)
                        {
                            if (dotProducts == null)
                                dotProducts = new double[numPeaks];
                            dotProducts[i] = dotProductCurrent;
                        }
                    }
                }
            }

            // Create graph items
            int iColor = 0;
            int lineWidth = LineWidth;
            float fontSize = FontSize;
            // We want the product ion colors to stay the same whether they are displayed:
            // 1. In a single pane with the precursor ions (Transitions -> All)
            // 2. In a separate pane of the split graph (Transitions -> All AND Transitions -> Split Graph)
            // 3. In a single pane by themselves (Transition -> Products)
            // We will use an offset in the colors array for cases 2 and 3 so that we do not reuse the precursor ion colors.
            var nodeDisplayType = GetDisplayType(DocumentUI, nodeGroup);
            int colorOffset = 0;
            if(displayType == DisplayTypeChrom.products && 
                (nodeDisplayType != DisplayTypeChrom.single || 
                 chromatograms.OptimizationFunction == null))
            {
                colorOffset = GetDisplayTransitions(nodeGroup, DisplayTypeChrom.precursors).Count();
            }

            for (int i = 0; i < numTrans; i++)
            {
                var info = arrayChromInfo[i];
                if (info == null && !allowEmpty)
                    continue;

                var nodeTran = displayTrans[i];
                if (!IsQuantitative(nodeTran) && Settings.Default.ShowQuantitativeOnly)
                {
                    continue;
                }
                int step = numSteps != 0 ? i - numSteps : 0;

                Color color;
                bool isSelected = false;
                int width = lineWidth;
                if ((numSteps == 0 && ReferenceEquals(nodeTran, nodeTranSelected) ||
                     (numSteps > 0 && step == 0)))
                {
                    color = ColorScheme.ChromGraphItemSelected;
                    isSelected = true;
                    width++;
                }
                else
                {
                    color = COLORS_LIBRARY[(iColor + colorOffset) % COLORS_LIBRARY.Count];
                }

                TransitionChromInfo tranPeakInfoGraph = null;
                if (bestPeakTran == i)
                    tranPeakInfoGraph = tranPeakInfo;

                var scanName = nodeTran.FragmentIonName;
                if (nodeTran.Transition.Adduct != Adduct.SINGLY_PROTONATED)  // Positive singly charged is uninteresting
                    scanName += Transition.GetChargeIndicator(nodeTran.Transition.Adduct);
                if (nodeTran.Transition.MassIndex != 0)
                    scanName += Environment.NewLine + Transition.GetMassIndexText(nodeTran.Transition.MassIndex);
                var fullScanInfo = new FullScanInfo
                {
                    ChromInfo = info,
                    ScanName = scanName
                };
                if (fullScanInfo.ChromInfo != null && fullScanInfo.ChromInfo.ExtractionWidth > 0)
                    _enableTrackingDot = true;
                // In order to display raw times within the defined peak bound we need to pass the
                // ChromGraphItem the bounds as it does not have access to that information
                RawTimesInfoItem? rawTimeInfo = null;
                if (isSelected && Settings.Default.ChromShowRawTimes)
                {
                    var retentionTimeValues = bestQuantitativePeak ?? bestNonQuantitativePeak;
                    if (retentionTimeValues != null)
                    {
                        rawTimeInfo = new RawTimesInfoItem()
                        {
                            StartBound = retentionTimeValues.StartRetentionTime,
                            EndBound = retentionTimeValues.EndRetentionTime
                        };
                    }
                }

                DashStyle dashStyle = IsQuantitative(nodeTran) ? DashStyle.Solid : DashStyle.Dot;
                var graphItem = new ChromGraphItem(nodeGroup,
                    nodeTran,
                    info,
                    tranPeakInfoGraph,
                    timeRegressionFunction,
                    GetAnnotationFlags(i, maxPeakTrans, maxPeakHeights),
                    dotProducts,
                    bestProduct,
                    isFullScanMs,
                    false,
                    rawTimeInfo,
                    step,
                    color,
                    fontSize,
                    width,
                    fullScanInfo)
                {
                    LineDashStyle = dashStyle,
                };
                _graphHelper.AddChromatogram(graphPaneKey, graphItem);
                if (isSelected)
                {
                    ShadeGraph(tranPeakInfo,info,timeRegressionFunction,dotProducts,bestProduct,isFullScanMs,step,fontSize,width,dashStyle,fullScanInfo,graphPaneKey);
                }
                iColor++;
            }

            var graphPane = _graphHelper.GetGraphPane(graphPaneKey);
            if (graphPane == null)
                _enableTrackingDot = false;
            if (_enableTrackingDot)
            {
                graphPane.CurveList.Insert(FULLSCAN_TRACKING_INDEX, CreateScanPoint(Color.Black));
                graphPane.CurveList.Insert(FULLSCAN_SELECTED_INDEX, CreateScanPoint(Color.Red));
            }
        }

        private void ShadeGraph(TransitionChromInfo tranPeakInfo, ChromatogramInfo chromatogramInfo,
            RegressionLine timeRegressionFunction, double[] dotProducts, double bestProduct, bool isFullScanMs,
            int step, float fontSize, int width, DashStyle dashStyle, FullScanInfo fullScanInfo, PaneKey graphPaneKey)
        {
            if (tranPeakInfo == null)
                return; // Nothing to shade
            float end = tranPeakInfo.EndRetentionTime;
            float start = tranPeakInfo.StartRetentionTime;
            double[] allTimes;
            double[] allIntensities;
            chromatogramInfo.AsArrays(out allTimes, out allIntensities);

            var peakTimes = new List<float>();
            var peakIntensities = new List<float>();
            for (int j = 0; j < allTimes.Length; j++)
            {
                if (start > allTimes[j])
                    continue;
                if (end < allTimes[j])
                    break;
                peakTimes.Add((float) allTimes[j]);
                peakIntensities.Add((float) allIntensities[j]);
            }
            if (peakIntensities.Count == 0)
                return;

            // Add peak area shading
            float[] peakTimesArray = peakTimes.ToArray();
            var infoPeakShade = new ChromatogramInfo(peakTimesArray, peakIntensities.ToArray());
            Assume.AreEqual(0, infoPeakShade.NumPeaks);
            var peakShadeItem = new ChromGraphItem(null,
                null,
                infoPeakShade,
                null,
                timeRegressionFunction,
                new bool[0],
                dotProducts,
                bestProduct,
                isFullScanMs,
                false,
                null,
                step,
                ColorScheme.ChromGraphItemSelected,
                fontSize,
                width,
                fullScanInfo)
            {
                LineDashStyle = dashStyle
            };
            var peakShadeCurveItem = _graphHelper.AddChromatogram(graphPaneKey, peakShadeItem);
            peakShadeCurveItem.Label.IsVisible = false;
            var lineItem = peakShadeCurveItem as LineItem;
            if (lineItem != null)
            {
                const int fillAlpha = 50;
                lineItem.Line.Fill = new Fill(Color.FromArgb(fillAlpha, lineItem.Color));
            }

            if (PeakIntegrator.HasBackgroundSubtraction(
                    DocumentUI.Settings.TransitionSettings.FullScan.AcquisitionMethod, chromatogramInfo.TimeIntervals,
                    chromatogramInfo.Source))
            {
                // Add peak background shading
                float min = Math.Min(peakIntensities.First(), peakIntensities.Last());
                var infoBackgroundShade = new ChromatogramInfo(peakTimesArray,
                    peakIntensities.Select(intensity => Math.Min(intensity, min)).ToArray());
                var backgroundShadeItem = new ChromGraphItem(null,
                    null,
                    infoBackgroundShade,
                    null,
                    timeRegressionFunction,
                    new bool[0],
                    dotProducts,
                    bestProduct,
                    isFullScanMs,
                    false,
                    null,
                    step,
                    Color.DarkGray,
                    fontSize,
                    2,
                    fullScanInfo);
                var backgroundShadeCurveItem = _graphHelper.AddChromatogram(graphPaneKey, backgroundShadeItem);
                backgroundShadeCurveItem.Label.IsVisible = false;
                var lineItem2 = backgroundShadeCurveItem as LineItem;
                if (lineItem2 != null)
                {
                    const int fillAlpha = 70;
                    lineItem2.Line.Fill = new Fill(Color.FromArgb(fillAlpha, Color.Black));
                }
            }
        }

        private LineItem CreateScanPoint(Color color)
        {
            return new LineItem(string.Empty, new[] { 0.0 }, new[] { 0.0 }, color, SymbolType.Circle)
            {
                Symbol =
                {
                    Size = FullScanPointSize,
                    Fill = new Fill(Color.Black),
                    IsAntiAlias = true,
                    Border = { Color = color, IsAntiAlias = true, Width = 2 }
                },
                Label = { IsVisible = false },
                IsVisible = false
            };
        }

        private void DisplayOptimizationTotals(RegressionLine timeRegressionFunction,
                                               ChromatogramSet chromatograms,
                                               float mzMatchTolerance,
                                               out RetentionTimeValues bestPeakTimes)
        {
            bestPeakTimes = null;
            for (int i = 0; i < _nodeGroups.Length; i++)
            {
                TransitionGroupDocNode nodeGroup = _nodeGroups[i];
                ChromatogramGroupInfo groupInfo = ChromGroupInfos[i];
                if (nodeGroup == null || groupInfo == null)
                {
                    continue;
                }
                List<ChromGraphItem> chromGraphItems = GetOptimizationTotalGraphItems(timeRegressionFunction,
                    chromatograms, mzMatchTolerance,
                    nodeGroup, groupInfo, out bestPeakTimes);
                foreach (var graphItem in chromGraphItems)
                {
                    _graphHelper.AddChromatogram(new PaneKey(nodeGroup), graphItem);
                }
            }
        }

        private List<ChromGraphItem> GetOptimizationTotalGraphItems(
            RegressionLine timeRegressionFunction,
            ChromatogramSet chromatograms,
            float mzMatchTolerance,
            TransitionGroupDocNode nodeGroup,
            ChromatogramGroupInfo chromGroupInfo,
            out RetentionTimeValues bestPeakTimes)
        {
            bestPeakTimes = null;
            List<ChromGraphItem> chromGraphItems = new List<ChromGraphItem>();
            float fontSize = FontSize;
            int lineWidth = LineWidth;
            int iColor = 0;

            ChromFileInfoId fileId = chromatograms.FindFile(chromGroupInfo);

            int numPeaks = chromGroupInfo.NumPeaks;

            // Collect the chromatogram info for the transition children
            // of this transition group.
            var listChromInfoSets = nodeGroup.Transitions.Select(transition =>
                chromGroupInfo.GetAllTransitionInfo(transition, mzMatchTolerance, chromatograms.OptimizationFunction,
                    TransformChrom.raw)).ToList();
            int totalSteps = chromatograms.OptimizationFunction.StepCount;

            // Enumerate optimization steps, grouping the data into graph data by step
            var listGraphData = new List<OptimizationGraphData>();
            for (int step = -totalSteps; step <= totalSteps; step++)
            {
                var optimizationData = new OptimizationGraphData(chromGroupInfo.NumPeaks);
                for (int iTransition = 0; iTransition < nodeGroup.TransitionCount; iTransition++)
                {
                    var optStepChromatograms = listChromInfoSets[iTransition];
                    if (optStepChromatograms == null)
                    {
                        continue;
                    }

                    var transitionDocNode = (TransitionDocNode) nodeGroup.Children[iTransition];
                    var transitionChromInfo = GetTransitionChromInfo(transitionDocNode, _chromIndex, fileId, step);
                    optimizationData.Add(optStepChromatograms.GetChromatogramForStep(step), transitionChromInfo);
                }
                listGraphData.Add(optimizationData);
            }

            // Total and transform the data, and compute which optimization
            // set has the most intense peak for each peak group.
            int bestPeakData = -1;
            TransitionChromInfo tranPeakInfo = null;
            float maxPeakHeight = float.MinValue;
            var maxPeakData = new int[numPeaks];
            var maxPeakHeights = new float[numPeaks];
            for (int i = 0; i < numPeaks; i++)
                maxPeakHeights[i] = float.MinValue;
            var transform = Transform;
            for (int i = 0; i < listGraphData.Count; i++)
            {
                var graphData = listGraphData[i];
                var infoPrimary = graphData.InfoPrimary;
                if (infoPrimary == null)
                    continue;

                // Sum intensities of all transitions in this
                // optimization bucket
                infoPrimary.SumIntensities(graphData.ChromInfos);

                // Apply any transform the user has chosen
                infoPrimary.Transform(transform);

                for (int j = 0; j < numPeaks; j++)
                {
                    float height = graphData.PeakHeights[j];
                    if (height > maxPeakHeights[j])
                    {
                        maxPeakHeights[j] = height;
                        maxPeakData[j] = i;
                    }
                }

                if (maxPeakHeight < graphData.TotalHeight)
                {
                    maxPeakHeight = graphData.TotalHeight;
                    bestPeakData = i;
                    tranPeakInfo = graphData.TransitionInfoPrimary;
                }
                bestPeakTimes = RetentionTimeValues.Merge(bestPeakTimes, RetentionTimeValues.FromTransitionChromInfo(graphData.TransitionInfoPrimary));
            }

            // Hide all peaks between the best peak extents
            if (tranPeakInfo != null)
            {
                for (int j = 0; j < numPeaks; j++)
                {
                    if (maxPeakHeights[j] == 0)
                        continue;
                    var graphData = listGraphData[maxPeakData[j]];
                    if (graphData.InfoPrimary == null)
                        continue;

                    ChromPeak peak = graphData.InfoPrimary.GetPeak(j);
                    if (peak.IsForcedIntegration)
                        continue;

                    if (IntersectPeaks(peak, tranPeakInfo))
                        maxPeakHeights[j] = 0;
                }
            }

            // Create graph items
            for (int i = 0; i < listGraphData.Count; i++)
            {
                var graphData = listGraphData[i];

                if (graphData.InfoPrimary != null || totalSteps > 0)    // Show everything for optimization runs
                {
                    int step = i - totalSteps;
                    int width = lineWidth;
                    Color color;
                    if (step == 0)
                    {
                        color = ColorScheme.ChromGraphItemSelected;
                        width++;
                    }
                    else if (nodeGroup.HasLibInfo)
                        color = COLORS_LIBRARY[iColor % COLORS_LIBRARY.Count];
                    else
                        color = COLORS_LIBRARY[iColor % COLORS_LIBRARY.Count];
                    //                                color = COLORS_HEURISTIC[iColor % COLORS_HEURISTIC.Length];

                    TransitionChromInfo tranPeakInfoGraph = null;
                    if (bestPeakData == i)
                        tranPeakInfoGraph = tranPeakInfo;
                    var graphItem = new ChromGraphItem(nodeGroup,
                                                       null,
                                                       graphData.InfoPrimary,
                                                       tranPeakInfoGraph,
                                                       timeRegressionFunction,
                                                       GetAnnotationFlags(i, maxPeakData, maxPeakHeights),
                                                       null,
                                                       0,
                                                       false,
                                                       false,
                                                       null,
                                                       step,
                                                       color,
                                                       fontSize,
                                                       width);
                    chromGraphItems.Add(graphItem);
                }

                iColor++;
            }
            return chromGraphItems;
        }

        private sealed class OptimizationGraphData
        {
            public OptimizationGraphData(int numPeaks)
            {
                ChromInfos = new List<ChromatogramInfo>();
                PeakHeights = new float[numPeaks];
            }

            public List<ChromatogramInfo> ChromInfos { get; private set; }
            public float TotalHeight { get; private set; }
            private float MaxHeight { get; set; }
            public ChromatogramInfo InfoPrimary { get; private set; }
            public TransitionChromInfo TransitionInfoPrimary { get; private set; }
            public float[] PeakHeights { get; private set; }

            public void Add(ChromatogramInfo chromInfo, TransitionChromInfo transitionChromInfo)
            {
                ChromInfos.Add(chromInfo);
                if (transitionChromInfo != null)
                {
                    float height = transitionChromInfo.Height;
                    TotalHeight += height;
                    if (height > MaxHeight || InfoPrimary == null)
                    {
                        MaxHeight = height;
                        InfoPrimary = chromInfo;
                        TransitionInfoPrimary = transitionChromInfo;
                    }
                }

                if (chromInfo != null)
                {
                    // Sum peak heights.  This may not be strictly valid, but should
                    // work as a good approximation for deciding which peaks to label.
                    int i = 0;
                    foreach (var peak in chromInfo.Peaks)
                    {
                        // Exclude any peaks between the boundaries of the chosen peak.
                        if (transitionChromInfo != null &&
                            transitionChromInfo.StartRetentionTime < peak.RetentionTime &&
                            peak.RetentionTime < transitionChromInfo.EndRetentionTime)
                            continue;
                        if (peak.IsForcedIntegration)
                            continue;

                        PeakHeights[i++] += peak.Height;
                    }
                }
            }
        }

        private void DisplayTotals(RegressionLine timeRegressionFunction,
                                   ChromatogramSet chromatograms,
                                   float mzMatchTolerance,
                                   int countLabelTypes,
                                   out RetentionTimeValues bestPeakTimes)
        {
            bestPeakTimes = null;
            // Construct and add graph items for all relevant transition groups.
            float fontSize = FontSize;
            int lineWidth = LineWidth;
            int iCharge = -1;
            var charge = Adduct.EMPTY;
            var chromGroupInfos = ChromGroupInfos;
            for (int i = 0; i < _nodeGroups.Length; i++)
            {
                var nodeGroup = _nodeGroups[i];
                var chromGroupInfo = chromGroupInfos[i];
                if (chromGroupInfo == null)
                    continue;

                ChromFileInfoId fileId = chromatograms.FindFile(chromGroupInfo);

                // Collect the chromatogram info for the transition children
                // of this transition group.
                ChromatogramInfo infoPrimary = null;
                TransitionChromInfo tranPeakInfo = null;
                float maxPeakHeight = float.MinValue;
                var listChromInfo = new List<ChromatogramInfo>();
                bool anyQuantitative = nodeGroup.Transitions.Any(IsQuantitative);
                foreach (TransitionDocNode nodeTran in nodeGroup.Children)
                {
                    if (anyQuantitative && !IsQuantitative(nodeTran))
                    {
                        continue;
                    }
                    var info = chromGroupInfo.GetTransitionInfo(nodeTran, mzMatchTolerance, TransformChrom.raw, chromatograms.OptimizationFunction);
                    if (info == null)
                        continue;

                    listChromInfo.Add(info);

                    // Keep track of which chromatogram owns the tallest member of
                    // the peak on the document tree.
                    var transitionChromInfo = GetTransitionChromInfo(nodeTran, _chromIndex, fileId, 0);
                    if (transitionChromInfo == null)
                        continue;

                    if (transitionChromInfo.Height > maxPeakHeight)
                    {
                        maxPeakHeight = transitionChromInfo.Height;
                        tranPeakInfo = transitionChromInfo;
                        infoPrimary = info;
                    }

                    // Adjust best peak window used to zoom the graph to the best peak
                    bestPeakTimes = RetentionTimeValues.Merge(bestPeakTimes, RetentionTimeValues.FromTransitionChromInfo(transitionChromInfo));
                }

                // If any transitions are present for this group, add a graph item to
                // the graph for it.
                if (listChromInfo.Count > 0)
                {
                    if (infoPrimary == null)
                        infoPrimary = listChromInfo[0];

                    // Sum the intensities of all transitions into the first chromatogram
                    infoPrimary.SumIntensities(listChromInfo);

                    // Apply any transform the user has chosen
                    infoPrimary.Transform(Transform);

                    int iColor = GetColorIndex(nodeGroup, countLabelTypes, ref charge, ref iCharge);
                    Color color = COLORS_GROUPS[iColor % COLORS_GROUPS.Count];

                    bool[] annotateAll = new bool[infoPrimary.NumPeaks];
                    for (int j = 0; j < annotateAll.Length; j++)
                    {
                        var peak = infoPrimary.GetPeak(j);
                        if (peak.IsForcedIntegration)
                            continue;

                        // Exclude any peaks between the boundaries of the chosen peak.
                        if (tranPeakInfo != null &&
                            tranPeakInfo.StartRetentionTime < peak.RetentionTime &&
                            peak.RetentionTime < tranPeakInfo.EndRetentionTime)
                            continue;
                        annotateAll[j] = true;
                    }
                    var graphItem = new ChromGraphItem(nodeGroup,
                                                       null,
                                                       infoPrimary,
                                                       tranPeakInfo,
                                                       timeRegressionFunction,
                                                       annotateAll,
                                                       null,
                                                       0,
                                                       false,
                                                       false,
                                                       null,
                                                       0,
                                                       color,
                                                       fontSize,
                                                       lineWidth)
                    {
                        LineDashStyle = anyQuantitative ? DashStyle.Solid : DashStyle.Dot,
                    };
                    var graphPaneKey = new PaneKey(nodeGroup);
                    _graphHelper.AddChromatogram(graphPaneKey, graphItem);
                }
            }
        }

        private bool IsQuantitative(TransitionDocNode transitionDocNode)
        {
            return transitionDocNode.IsQuantitative(DocumentUI.Settings);
        }

        private class DisplayPeptide
        {
            public int PeptideIndex;
            public ChromatogramInfo SumInfo;
            public TransitionChromInfo BestPeakInfo;
        }

        /// <summary>
        /// Display summed transitions for multiple selected peptides.
        /// </summary>
        private void DisplayPeptides(RegressionLine timeRegressionFunction,
                                   ChromatogramSet chromatograms,
                                   float mzMatchTolerance,
                                   int countLabelTypes,
                                   IList<PeptideDocNode> peptideDocNodes,
                                   out RetentionTimeValues firstPeak,
                                   out RetentionTimeValues lastPeak)
        {
            firstPeak = null;
            lastPeak = null;

            // Construct and add graph items for all relevant transition groups.
            float fontSize = FontSize;
            int lineWidth = LineWidth;
            var chromGroupInfos = ChromGroupInfos;
            var lookupChromGroupInfoIndex = new Dictionary<int, int>(_nodeGroups.Length);
            for (int i = 0; i < _nodeGroups.Length; i++)
                lookupChromGroupInfoIndex[_nodeGroups[i].Id.GlobalIndex] = i;

            // Generate a unique short identifier for each peptide.
            var peptideNames = new Tuple<string,bool>[peptideDocNodes.Count];
            for (int i = 0; i < peptideDocNodes.Count; i++)
                peptideNames[i] = new Tuple<string,bool>(peptideDocNodes[i].ModifiedTarget.DisplayName, peptideDocNodes[i].IsProteomic);
            var uniqueNames = new UniquePrefixGenerator(peptideNames, 3);

            var displayPeptides = new List<DisplayPeptide>();
            for (int peptideIndex = 0; peptideIndex < peptideDocNodes.Count; peptideIndex++)
            {
                var peptideDocNode = peptideDocNodes[peptideIndex];
                TransitionChromInfo bestPeakInfo = null;
                ChromatogramInfo sumInfo = null;
                float maxPeakHeight = float.MinValue;

                foreach (var precursor in peptideDocNode.TransitionGroups)
                {
                    int indexInfo;
                    if (!lookupChromGroupInfoIndex.TryGetValue(precursor.Id.GlobalIndex, out indexInfo))
                        continue;
                    var chromGroupInfo = chromGroupInfos[indexInfo];
                    if (chromGroupInfo == null)
                        continue;
                    ChromFileInfoId fileId = chromatograms.FindFile(chromGroupInfo);
                    foreach (var nodeTran in precursor.Transitions)
                    {
                        var info = chromGroupInfo.GetTransitionInfo(nodeTran, mzMatchTolerance, TransformChrom.raw, chromatograms.OptimizationFunction);
                        if (info == null)
                            continue;
                        if (sumInfo == null)
                            sumInfo = info;
                        else
                        {
                            float[] sumTimes;
                            float[] sumIntensities;
                            // TODO(toaarray)
                            if (!AddTransitions.Add(
                                info.Times.ToArray(), info.Intensities.ToArray(), sumInfo.Times.ToArray(), sumInfo.Intensities.ToArray(),
                                out sumTimes, out sumIntensities))
                                continue;
                            sumInfo = new ChromatogramInfo(sumTimes, sumIntensities);
                        }

                        // Keep track of which chromatogram owns the tallest member of
                        // the peak on the document tree.
                        var transitionChromInfo = GetTransitionChromInfo(nodeTran, _chromIndex, fileId, 0);
                        if (transitionChromInfo == null)
                            continue;

                        if (transitionChromInfo.Height > maxPeakHeight)
                        {
                            maxPeakHeight = transitionChromInfo.Height;
                            bestPeakInfo = transitionChromInfo;
                        }
                    }
                }

                if (sumInfo != null && bestPeakInfo != null)
                {
                    displayPeptides.Add(new DisplayPeptide
                    {
                        PeptideIndex = peptideIndex,
                        SumInfo = sumInfo,
                        BestPeakInfo = bestPeakInfo
                    });
                }
            }

            // Order the peptides by height of best peak.
            displayPeptides = displayPeptides.OrderByDescending(e => e.BestPeakInfo.Height).ToList();

            // Display only the top peptides.
            int lastPeptideIndex = Math.Min(MaxPeptidesDisplayed, displayPeptides.Count);
            var graphItems = new List<ChromGraphItem>();
            for (int i = lastPeptideIndex-1; i >= 0; i--) // smallest peaks first for good z-ordering in graph
            {
                var bestPeakInfo = displayPeptides[i].BestPeakInfo;
                var sumInfo = displayPeptides[i].SumInfo;
                var peptideDocNode = peptideDocNodes[displayPeptides[i].PeptideIndex];

                // Intersect best peak with summed transition.
                if (bestPeakInfo != null && sumInfo.Times != null && sumInfo.Times.Count > 0)
                {
                    float startRetentionTime = Math.Max(bestPeakInfo.StartRetentionTime, sumInfo.Times[0]);
                    float endRetentionTime = Math.Min(bestPeakInfo.EndRetentionTime, sumInfo.Times[sumInfo.Times.Count - 1]);
                    if (endRetentionTime > startRetentionTime)
                    {
                        bestPeakInfo = new TransitionChromInfo(startRetentionTime, endRetentionTime);
                        var retentionTimeValues = RetentionTimeValues.FromTransitionChromInfo(bestPeakInfo);
                        if (retentionTimeValues != null)
                        {
                            if (firstPeak == null ||
                                firstPeak.StartRetentionTime > retentionTimeValues.StartRetentionTime)
                            {
                                firstPeak = retentionTimeValues;
                            }

                            if (lastPeak == null || lastPeak.EndRetentionTime < retentionTimeValues.EndRetentionTime)
                            {
                                lastPeak = retentionTimeValues;
                            }
                        }
                    }
                }

                // Get peptide graph color from SequenceTree.
                var peptideGraphInfo = _stateProvider.GetPeptideGraphInfo(peptideDocNode);
                Color color = peptideGraphInfo.Color;

                sumInfo.Transform(Transform);
                bool[] annotateAll = new bool[sumInfo.NumPeaks];
                ChromGraphItem graphItem = new ChromGraphItem(null,
                    null,
                    sumInfo,
                    bestPeakInfo,
                    timeRegressionFunction,
                    annotateAll,
                    null,
                    0,
                    false,
                    false,
                    null,
                    0,
                    color,
                    fontSize,
                    lineWidth)
                {
                    CurveAnnotation = uniqueNames.GetUniquePrefix(peptideDocNode.ModifiedTarget.DisplayName, peptideDocNode.IsProteomic),
                    IdPath = _groupPaths[displayPeptides[i].PeptideIndex],
                    GraphInfo = peptideGraphInfo
                };
                if (peptideGraphInfo.IsSelected)
                    graphItems.Insert(0, graphItem);
                else
                    graphItems.Add(graphItem);
            }

            // ReSharper disable PossibleInvalidCastException
            foreach (var graphItem in graphItems)
            {
                var curveItem = _graphHelper.AddChromatogram(new PaneKey(), graphItem);
                // Make the fill color under the curve more opaque if the curve is selected.
                var fillAlpha = graphItem.GraphInfo.IsSelected ? 60 : 15;
                ((LineItem)curveItem).Line.Fill = new Fill(Color.FromArgb(fillAlpha, graphItem.GraphInfo.Color));
            }
            // ReSharper enable PossibleInvalidCastException
        }

        private void SetRetentionTimeIndicators(ChromGraphItem chromGraphPrimary,
                                                SrmSettings settings,
                                                ChromatogramSet chromatograms,
                                                PeptideDocNode[] nodePeps,
                                                TransitionGroupDocNode[] nodeGroups,
                                                Target lookupSequence,
                                                ExplicitMods lookupMods)
        {
            if (lookupSequence == null)
            {
                return;
            }
            SetRetentionTimePredictedIndicator(chromGraphPrimary, settings, chromatograms,
                nodePeps, lookupSequence, lookupMods);
            SetRetentionTimeIdIndicators(chromGraphPrimary, settings,
                nodeGroups, lookupSequence, lookupMods);
        }

        private static void SetRetentionTimePredictedIndicator(ChromGraphItem chromGraphPrimary,
                                                               SrmSettings settings,
                                                               ChromatogramSet chromatograms,
                                                               PeptideDocNode[] nodePeps,
                                                               Target lookupSequence,
                                                               ExplicitMods lookupMods)
        {
            // Set predicted retention time on the first graph item to make
            // line, label and shading show.
            var regression = settings.PeptideSettings.Prediction.RetentionTime;
            if (regression != null && Settings.Default.ShowRetentionTimePred)
            {
                var modSeq = settings.GetModifiedSequence(lookupSequence, IsotopeLabelType.light, lookupMods);
                var fileId = chromatograms.FindFile(chromGraphPrimary.Chromatogram.GroupInfo);
                double? predictedRT = regression.GetRetentionTime(modSeq, fileId);
                double window = regression.TimeWindow;

                chromGraphPrimary.RetentionPrediction = predictedRT;
                chromGraphPrimary.RetentionWindow = window;
            }
            if (nodePeps != null && nodePeps.Any() && nodePeps.All(np => Equals(np.ExplicitRetentionTime, nodePeps[0].ExplicitRetentionTime)))
            {
                chromGraphPrimary.RetentionExplicit = nodePeps[0].ExplicitRetentionTime;
            }
            else
            {
                chromGraphPrimary.RetentionExplicit = null;
            }
        }


        private void SetRetentionTimeIdIndicators(ChromGraphItem chromGraphPrimary,
                                                  SrmSettings settings,
                                                  IEnumerable<TransitionGroupDocNode> nodeGroups,
                                                  Target lookupSequence,
                                                  ExplicitMods lookupMods)
        {
            // Set any MS/MS IDs on the first graph item also
            if (settings.PeptideSettings.Libraries.IsLoaded &&
                (settings.TransitionSettings.FullScan.IsEnabled || settings.PeptideSettings.Libraries.HasMidasLibrary))
            {
                var nodeGroupsArray = nodeGroups.ToArray();
                var transitionGroups = nodeGroupsArray.Select(nodeGroup => nodeGroup.TransitionGroup).ToArray();
                if (Settings.Default.ShowPeptideIdTimes)
                {
                    var listTimes = new List<double>();
                    foreach (var group in transitionGroups)
                    {
                        IsotopeLabelType labelType;
                        double[] retentionTimes;
                        if (settings.TryGetRetentionTimes(lookupSequence, group.PrecursorAdduct,
                                                          lookupMods, FilePath, out labelType, out retentionTimes))
                        {
                            listTimes.AddRange(retentionTimes);
                        }
                    }
                    var selectedSpectrum = _stateProvider.SelectedSpectrum;
                    if (selectedSpectrum != null && Equals(FilePath, selectedSpectrum.FilePath))
                    {
                        chromGraphPrimary.SelectedRetentionMsMs = selectedSpectrum.RetentionTime;
                    }
                    if (listTimes.Count > 0)
                        chromGraphPrimary.RetentionMsMs = listTimes.ToArray();

                    if (settings.PeptideSettings.Libraries.HasMidasLibrary && _stateProvider.SelectedNode != null)
                    {
                        PeptideDocNode nodePep = null;
                        TransitionGroupDocNode nodeTranGroup = null;
                        switch (_stateProvider.SelectedNode)
                        {
                            case TransitionGroupTreeNode treeNodeTranGroup:
                                nodePep = treeNodeTranGroup.PepNode;
                                nodeTranGroup = treeNodeTranGroup.DocNode;
                                break;
                            case TransitionTreeNode treeNodeTran:
                                nodePep = treeNodeTran.PepNode;
                                nodeTranGroup = treeNodeTran.TransitionGroupNode;
                                break;
                        }

                        if (nodePep != null && nodeTranGroup != null)
                        {
                            var libKey = new LibKey(nodePep.ModifiedTarget, nodeTranGroup.PrecursorAdduct);
                            chromGraphPrimary.MidasRetentionMsMs = settings.PeptideSettings.Libraries.MidasLibraries
                                .SelectMany(lib => lib.GetSpectraByPeptide(chromGraphPrimary.Chromatogram.FilePath, libKey))
                                .Select(s => s.RetentionTime).ToArray();
                        }
                    }
                }
                if (Settings.Default.ShowAlignedPeptideIdTimes)
                {
                    var listTimes = new List<double>(settings.GetAlignedRetentionTimes(FilePath, lookupSequence, lookupMods));
                    if (listTimes.Count > 0)
                    {
                        var sortedTimes = listTimes.Distinct().ToArray();
                        Array.Sort(sortedTimes);
                        chromGraphPrimary.AlignedRetentionMsMs = sortedTimes;
                    }
                }
                if (Settings.Default.ShowUnalignedPeptideIdTimes)
                {
                    var precursorMzs = nodeGroupsArray.Select(nodeGroup => nodeGroup.PrecursorMz).ToArray();
                    var listTimes = new List<double>(settings.GetRetentionTimesNotAlignedTo(FilePath, lookupSequence, lookupMods, precursorMzs));
                    if (listTimes.Count > 0)
                    {
                        var sortedTimes = listTimes.Distinct().ToArray();
                        Array.Sort(sortedTimes);
                        chromGraphPrimary.UnalignedRetentionMsMs = sortedTimes;
                    }
                }
            }
        }

        private static bool IntersectPeaks(ChromPeak peak, TransitionChromInfo chromInfo)
        {
            if (chromInfo == null)
                return false;

            // Allow start and end to share the same time, but nothing more.
            return Math.Min(chromInfo.EndRetentionTime, peak.EndTime) -
                   Math.Max(chromInfo.StartRetentionTime, peak.StartTime) > 0;
        }

        private static TransitionChromInfo GetTransitionChromInfo(TransitionDocNode nodeTran,
                                                                  int indexChrom,
                                                                  ChromFileInfoId fileId,
                                                                  int step)
        {
            if (!nodeTran.HasResults || nodeTran.Results.Count <= indexChrom)
                return null;
            var tranChromInfoList = nodeTran.Results[indexChrom];
            if (tranChromInfoList.IsEmpty)
                return null;
            foreach (var tranChromInfo in tranChromInfoList)
            {
                if (ReferenceEquals(tranChromInfo.FileId, fileId) && tranChromInfo.OptimizationStep == step)
                    return tranChromInfo;
            }
            return null;
        }

        private static TransitionGroupChromInfo GetTransitionGroupChromInfo(TransitionGroupDocNode nodeGroup,
                                                                            ChromFileInfoId fileId,
                                                                            int indexChrom)
        {
            if (!nodeGroup.HasResults || indexChrom >= nodeGroup.Results.Count)
                return null;
            var tranGroupChromInfoList = nodeGroup.Results[indexChrom];
            if (tranGroupChromInfoList.IsEmpty)
                return null;
            foreach (var tranGroupChromInfo in tranGroupChromInfoList)
            {
                if (ReferenceEquals(tranGroupChromInfo.FileId, fileId))
                    return tranGroupChromInfo;
            }
            return null;
        }

        private void UpdateToolbar(IList<ChromatogramGroupInfo[]> arrayChromInfo)
        {
            if (arrayChromInfo == null || arrayChromInfo.Count < 2)
            {
                if (toolBar.Visible)
                {
                    toolBar.Visible = false;
                    graphControl.Top = toolBar.Top;
                    graphControl.Height += toolBar.Height;
                }
            }
            else
            {
                // Check to see if the list of files has changed.
                var listNames = new List<string>();
                if (_hasMergedChromInfo)
                    listNames.Add(Resources.GraphChromatogram_UpdateToolbar_All);
                for (int i = _hasMergedChromInfo ? 1 : 0; i < arrayChromInfo.Count; i++)
                {
                    var arrayInfo = arrayChromInfo[i];
                    string name = string.Empty;
                    foreach (var info in arrayInfo)
                    {
                        if (info != null)
                        {
                            name = SampleHelp.GetPathSampleNamePart(info.FilePath);
                            if (string.IsNullOrEmpty(name))
                                name = info.FilePath.GetFileName();
                            break;
                        }
                    }
                    listNames.Add(name);
                }
                var listExisting = new List<string>();
                foreach (var item in comboFiles.Items)
                    listExisting.Add(item.ToString());
                if (!ArrayUtil.EqualsDeep(listNames, listExisting))
                {
                    // If it has, update the list, trying to maintain selection, if possible.
                    object selected = comboFiles.SelectedItem;
                    comboFiles.Items.Clear();
                    foreach (string name in listNames)
                        comboFiles.Items.Add(name);
                    _dontSyncSelectedFile = true;
                    if (selected == null || comboFiles.Items.IndexOf(selected) == -1 || 
                        (_hasMergedChromInfo && listNames[0] != listExisting[0]))
                        comboFiles.SelectedIndex = 0;
                    else
                        comboFiles.SelectedItem = selected;
                    _dontSyncSelectedFile = false;
                    ComboHelper.AutoSizeDropDown(comboFiles);
                }

                // Show the toolbar after updating the files
                if (!toolBar.Visible)
                {
                    toolBar.Visible = true;
                    graphControl.Top = toolBar.Bottom;
                    graphControl.Height -= toolBar.Height;
                }
            }
        }

        private bool EnsureChromInfo(MeasuredResults results,
                                     ChromatogramSet chromatograms,
                                     TransitionGroupDocNode[] nodeGroups,
                                     IdentityPath[] groupPaths,
                                     ChromExtractor extractor,
                                     out bool changedGroups,
                                     out bool changedGroupIds)
        {
            bool qcTraceNameMatches = extractor != ChromExtractor.qc ||
                                      (_arrayChromInfo != null &&
                                       _arrayChromInfo.Length > 0 &&
                                       _arrayChromInfo[0].Length > 0 &&
                                       _arrayChromInfo[0][0].TextId == Settings.Default.ShowQcTraceName);

            if (UpdateGroups(nodeGroups, groupPaths, out changedGroups, out changedGroupIds) &&
                _extractor == extractor &&
                qcTraceNameMatches &&
                ReferenceEquals(results, _measuredResults))
                return true;

            _extractor = extractor;

            bool success = false;
            try
            {
                // Get chromatogram sets for all transition groups, recording unique
                // file paths in the process.
                var listArrayChromInfo = new List<ChromatogramGroupInfo[]>();
                var listFiles = new List<MsDataFileUri>();
                ChromatogramGroupInfo[] arrayAllIonsChromInfo;
                if (!results.TryLoadAllIonsChromatogram(chromatograms, extractor, true,
                                                        out arrayAllIonsChromInfo))
                {
                    return false;
                }
                else
                {
                    listArrayChromInfo.Add(arrayAllIonsChromInfo);
                    foreach (var chromInfo in arrayAllIonsChromInfo)
                    {
                        var filePath = chromInfo.FilePath;
                        if (!listFiles.Contains(filePath))
                            listFiles.Add(filePath);
                    }
                }

                _hasMergedChromInfo = false;
                _arrayChromInfo = new ChromatogramGroupInfo[listFiles.Count][];
                _measuredResults = results;
                for (int i = 0; i < _arrayChromInfo.Length; i++)
                {
                    var arrayNew = new ChromatogramGroupInfo[listArrayChromInfo.Count];
                    for (int j = 0; j < arrayNew.Length; j++)
                    {
                        var arrayChromInfo = listArrayChromInfo[j];
                        if (arrayChromInfo == null)
                            continue;
                        foreach (var chromInfo in arrayChromInfo)
                        {
                            qcTraceNameMatches = extractor != ChromExtractor.qc || Settings.Default.ShowQcTraceName == chromInfo.TextId;
                            if (arrayNew[j] == null && Equals(listFiles[i], chromInfo.FilePath) && qcTraceNameMatches)
                                arrayNew[j] = chromInfo;
                        }
                    }
                    _arrayChromInfo[i] = arrayNew;
                }

                success = true;
            }
            finally
            {
                // Make sure the info array is set to null on failure.
                if (!success)
                {
                    _arrayChromInfo = null;
                    _measuredResults = null;
                }
            }

            return true;
        }

        private bool EnsureChromInfo(MeasuredResults results,
                                     ChromatogramSet chromatograms,
                                     PeptideDocNode[] nodePeps,
                                     TransitionGroupDocNode[] nodeGroups,
                                     IdentityPath[] groupPaths,
                                     float mzMatchTolerance,
                                     out bool changedGroups,
                                     out bool changedGroupIds)
        {
            if (UpdateGroups(nodeGroups, groupPaths, out changedGroups, out changedGroupIds) 
                && !_extractor.HasValue 
                && ReferenceEquals(results, _measuredResults))
                return true;

            _extractor = null;

            bool success = false;
            try
            {
                // Get chromatogram sets for all transition groups, recording unique
                // file paths in the process.
                var listArrayChromInfo = new List<ChromatogramGroupInfo[]>();
                var listFiles = new List<MsDataFileUri>();
                for (int i = 0; i < nodeGroups.Length; i++)
                {
                    if (!results.TryLoadChromatogram(
                        chromatograms, 
                        nodePeps[i], 
                        nodeGroups[i], 
                        mzMatchTolerance, 
                        out var arrayChromInfo))
                    {
                        listArrayChromInfo.Add(null);
                        continue;
                    }

                    listArrayChromInfo.Add(arrayChromInfo);
                    foreach (var chromInfo in arrayChromInfo)
                    {
                        var filePath = chromInfo.FilePath;
                        if (!listFiles.Contains(filePath))
                            listFiles.Add(filePath);
                    }
                }

                // If no data was found, then return false
                if (listFiles.Count == 0)
                    return false;

                // Make a list of chromatogram info by unique file path corresponding
                // to the groups passed in.
                _arrayChromInfo = new ChromatogramGroupInfo[listFiles.Count][];
                _measuredResults = results;
                for (int i = 0; i < _arrayChromInfo.Length; i++)
                {
                    var arrayNew = new ChromatogramGroupInfo[listArrayChromInfo.Count];
                    for (int j = 0; j < arrayNew.Length; j++)
                    {
                        var arrayChromInfo = listArrayChromInfo[j];
                        if (arrayChromInfo == null)
                            continue;
                        foreach (var chromInfo in arrayChromInfo)
                        {
                            if (arrayNew[j] == null && Equals(listFiles[i], chromInfo.FilePath))
                                arrayNew[j] = chromInfo;
                        }
                    }
                    _arrayChromInfo[i] = arrayNew;
                }

                // If multiple replicate files contain mutually exclusive data, create "all files" option.
                var mergedChromGroupInfo = GetMergedChromInfo();
                _hasMergedChromInfo = (mergedChromGroupInfo != null);
                if (_hasMergedChromInfo)
                {
                    var arrayNew = new ChromatogramGroupInfo[_arrayChromInfo.Length + 1][];
                    arrayNew[0] = mergedChromGroupInfo;
                    for (int i = 1; i < arrayNew.Length; i++)
                        arrayNew[i] = _arrayChromInfo[i - 1];
                    _arrayChromInfo = arrayNew;
                }

                success = true;
            }
            finally
            {
                // Make sure the info array is set to null on failure.
                if (!success)
                {
                    _arrayChromInfo = null;
                    _measuredResults = null;
                }
            }

            return true;
        }

        /// <summary>
        /// If multiple replicate files contain mutually exclusive chromatogram groups, create
        /// a merged chromatogram group.  If there are any collisions, return null.
        /// </summary>
        private ChromatogramGroupInfo[] GetMergedChromInfo()
        {
            if (!_showPeptideTotals || _arrayChromInfo.Length < 2)
                return null;

            var mergedChromGroupInfo = new ChromatogramGroupInfo[_arrayChromInfo[0].Length];
            for (int i = 0; i < _arrayChromInfo.Length; i++)
            {
                for (int j = 0; j < mergedChromGroupInfo.Length; j++)
                {
                    if (_arrayChromInfo[i][j] != null)
                    {
                        if (mergedChromGroupInfo[j] != null)
                            return null;
                        mergedChromGroupInfo[j] = _arrayChromInfo[i][j];
                    }
                }
            }

            return mergedChromGroupInfo;
        }

        /// <summary>
        /// Updates the transition group properties for the graph.
        /// </summary>
        /// <returns>True if groups are already up to date, False if they were changed</returns>
        private bool UpdateGroups(TransitionGroupDocNode[] nodeGroups, IdentityPath[] groupPaths,
                                  out bool changedGroups, out bool changedGroupIds)
        {
            changedGroups = false;
            changedGroupIds = false;
            if (ArrayUtil.ReferencesEqual(nodeGroups, _nodeGroups) && _arrayChromInfo != null)
                return true;

            changedGroups = true;
            int lenNew = nodeGroups.SafeLength();
            int lenOld = _nodeGroups.SafeLength();
            if (lenNew != lenOld)
                changedGroupIds = true;
            else
            {
                for (int i = 0; i < lenNew; i++)
                {
                    if (!ReferenceEquals(nodeGroups[i].Id, _nodeGroups[i].Id))
                        changedGroupIds = true;
                }
            }

            _nodeGroups = nodeGroups;
            _groupPaths = groupPaths;
            return false;
        }

        private static bool[] GetAnnotationFlags(int iTran, int[] maxPeakTrans, float[] maxPeakHeights)
        {
            var flags = new bool[maxPeakTrans.Length];
            for (int i = 0; i < maxPeakTrans.Length; i++)
                flags[i] = (iTran == maxPeakTrans[i] && maxPeakHeights[i] > 0);
            return flags;
        }

        protected override string GetPersistentString()
        {
            return base.GetPersistentString() + '|' + TabText;
        }

        public static string GetTabText(string persistentString)
        {
            string[] values = persistentString.Split('|');
            if (values.Length == 2)
                return values[1];
            return null;
        }

        #region Implementation of IGraphContainer

        public void LockYAxis(bool lockY)
        {
            graphControl.IsEnableVPan = graphControl.IsEnableVZoom = !lockY;
            graphControl.Refresh();
        }

        #endregion

        #region Editing support

        public IdentityPath FindIdentityPath(TextObj label)
        {
            foreach (var graphItem in GraphItems)
            {
                var idPath = graphItem.FindIdentityPath(label);
                if (idPath != null)
                    return idPath;
            }
            return null;
        }

        public ScaledRetentionTime FindAnnotatedPeakRetentionTime(double time,
                                                                   out TransitionGroupDocNode nodeGroup,
                                                                   out TransitionDocNode nodeTran)
        {
            foreach (var graphItem in GraphItems)
            {
                var peakRT = graphItem.FindPeakRetentionTime(time);
                if (!peakRT.IsZero)
                {
                    nodeGroup = graphItem.TransitionGroupNode;
                    nodeTran = graphItem.TransitionNode;
                    return peakRT;
                }
            }
            nodeGroup = null;
            nodeTran = null;
            return ScaledRetentionTime.ZERO;
        }

        private ScaledRetentionTime FindAnnotatedPeakRetentionTime(TextObj label,
                                                                   out TransitionGroupDocNode nodeGroup,
                                                                   out TransitionDocNode nodeTran)
        {
            foreach (var graphItem in GraphItems)
            {
                var peakRT = graphItem.FindPeakRetentionTime(label);
                if (!peakRT.IsZero)
                {
                    peakRT = GetRetentionTimeOfZeroOptStep(graphItem, peakRT);
                }
                if (!peakRT.IsZero) 
                {
                    nodeGroup = graphItem.TransitionGroupNode;
                    nodeTran = graphItem.TransitionNode;
                    return peakRT;
                }
            }
            nodeGroup = null;
            nodeTran = null;
            return ScaledRetentionTime.ZERO;
        }

        private ScaledRetentionTime GetRetentionTimeOfZeroOptStep(ChromGraphItem graphItem, ScaledRetentionTime peakTime)
        {
            if (graphItem.OptimizationStep == 0)
            {
                return peakTime;
            }
            ChromGraphItem mainGraphItem = GraphItems.FirstOrDefault(item =>
                0 == item.OptimizationStep 
                && ReferenceEquals(item.TransitionNode, graphItem.TransitionNode)
                && ReferenceEquals(item.TransitionGroupNode, graphItem.TransitionGroupNode));
            if (mainGraphItem == null)
            {
                return ScaledRetentionTime.ZERO;
            }
            int iPeak = graphItem.Chromatogram.IndexOfPeak(peakTime.MeasuredTime);
            ChromPeak mainPeak = mainGraphItem.Chromatogram.GetPeak(iPeak);
            return mainGraphItem.ScaleRetentionTime(mainPeak.RetentionTime);
        }

        private ScaledRetentionTime FindAnnotatedSpectrumRetentionTime(TextObj label)
        {
            foreach (var graphItem in GraphItems)
            {
                var spectrumRT = graphItem.FindSpectrumRetentionTime(label);
                if (!spectrumRT.IsZero)
                {
                    return spectrumRT;
                }
            }
            return ScaledRetentionTime.ZERO;
        }

        private ScaledRetentionTime FindAnnotatedSpectrumRetentionTime(LineObj line)
        {
            foreach (var graphItem in GraphItems)
            {
                var spectrumRT = graphItem.FindSpectrumRetentionTime(line);
                if (!spectrumRT.IsZero)
                {
                    return spectrumRT;
                }
            }
            return ScaledRetentionTime.ZERO;
        }

        private ChromGraphItem FindMaxPeakItem(GraphPane graphPane, ScaledRetentionTime startTime, ScaledRetentionTime endTime)
        {
            double maxInten = 0;
            ChromGraphItem maxItem = null;

            foreach (ChromGraphItem graphItemCurr in GetGraphItems(graphPane))
            {
                double inten = graphItemCurr.GetMaxIntensity(startTime.MeasuredTime, endTime.MeasuredTime);
                if (inten > maxInten)
                {
                    maxInten = inten;
                    maxItem = graphItemCurr;
                }
            }

            return maxItem;
        }

        private ChromGraphItem FindBestPeakItem(CurveItem curve)
        {
            ChromGraphItem graphItem = curve.Tag as ChromGraphItem;
            if (graphItem == null || graphItem.TransitionChromInfo != null)
                return graphItem;

            // Look for a transition from the same precursor with chrom info
            var nodeGroup = graphItem.TransitionGroupNode;
            foreach (var graphItemCurr in GraphItems)
            {
                if (ReferenceEquals(nodeGroup, graphItemCurr.TransitionGroupNode) &&
                    graphItemCurr.TransitionChromInfo != null)
                    return graphItemCurr;
            }

            // If nothing better found, use the current chromatogram.
            return graphItem;
        }

        private ScaledRetentionTime FindBestPeakTime(GraphPane graphPane, CurveItem curve, PointF pt, out ChromGraphItem graphItem)
        {
            graphItem = FindBestPeakItem(curve);
            if (graphItem != null)
            {
                double displayTime, yTemp;
                graphPane.ReverseTransform(pt, out displayTime, out yTemp);
                return graphItem.GetValidPeakBoundaryTime(displayTime);
            }

            return ScaledRetentionTime.ZERO;
        }

        private ScaledRetentionTime FindBestPeakBoundary(PointF pt, out GraphPane graphPane, out ChromGraphItem graphItem)
        {
            double deltaBest = double.MaxValue;
            ScaledRetentionTime timeBest = ScaledRetentionTime.ZERO;
            graphItem = null;
            ChromGraphItem graphItemBest = null;
            graphPane = GraphPaneFromPoint(pt);
            if (null != graphPane)
            {
                double time, yTemp;
                graphPane.ReverseTransform(pt, out time, out yTemp);

                foreach (var graphItemNext in GetGraphItems(graphPane))
                {
                    var transitionChromInfo = graphItemNext.TransitionChromInfo;
                    if (transitionChromInfo == null)
                    {
                        continue;
                    }
                    var timeMatch = graphItemNext.GetNearestBestPeakBoundary(time);
                    if (!timeMatch.IsZero)
                    {
                        double delta = Math.Abs(time - timeMatch.DisplayTime);
                        if (delta < deltaBest)
                        {
                            deltaBest = delta;
                            timeBest = timeMatch;
                            graphItemBest = graphItemNext;
                        }
                    }
                }

                // Only match if the best time is close enough in absolute pixels
                if (graphItemBest != null && Math.Abs(pt.X - graphPane.XAxis.Scale.Transform(timeBest.DisplayTime)) > 3)
                    graphItemBest = null;

                graphItem = graphItemBest;
            }
            if (graphItem == null)
            {
                return ScaledRetentionTime.ZERO;
            }
            return timeBest;
        }

        public IEnumerable<ChromGraphItem> GraphItems
        {
            get { return GraphPanes.SelectMany(GetGraphItems); }
        }

        public IEnumerable<ChromGraphItem> GetGraphItems(GraphPane pane)
        {
            return GetCurves(pane).Select(c => c.Tag).Cast<ChromGraphItem>();
        }

        /// <summary>
        /// Return graph items associated with a GraphPane's curves.
        /// </summary>
        public IEnumerable<CurveItem> GetCurves(GraphPane pane)
        {
            foreach (var curve in pane.CurveList)
            {
                var graphItem = curve.Tag as ChromGraphItem;
                if (graphItem != null)
                {
                    if (_showPeptideTotals || graphItem.TransitionGroupNode != null)
                        yield return curve;
                }
            }
        }

        /// <summary>
        /// Return a GraphPane's curve list, skipping curves that don't have a graph item.
        /// </summary>
        public CurveList GetCurveList(GraphPane pane)
        {
            var curveList = new CurveList();
            curveList.AddRange(GetCurves(pane));
            return curveList;
        }

        /// <summary>
        /// Return the curve list for the first GraphPane.
        /// </summary>
        public CurveList CurveList
        {
            get { return GetCurveList(GraphPanes.First()); }
        }

        public double[] RetentionMsMs
        {
            get { return GetTimes(g => g.RetentionMsMs); }
        }

        public double[] MidasRetentionMsMs
        {
            get { return GetTimes(g => g.MidasRetentionMsMs); }
        }

        public double[] AlignedRetentionMsMs
        {
            get { return GetTimes(g => g.AlignedRetentionMsMs); }
        }

        public double[] UnalignedRetentionMsMs
        {
            get { return GetTimes(g => g.UnalignedRetentionMsMs); }
        }

        private double[] GetTimes(Func<ChromGraphItem, double[]> getProp)
        {
            return (from graphItem in GraphItems
                    where getProp(graphItem) != null
                    select getProp(graphItem)).FirstOrDefault();
        }

        private PeakBoundsDragInfo[] _peakBoundDragInfos;

        private bool graphControl_MouseMoveEvent(ZedGraphControl sender, MouseEventArgs e)
        {
            HideFullScanTrackingPoint();
            var result = HandleMouseMove(e);
            if (result)
            {
                sender.Invalidate(sender.Region);
                sender.Update();
            }
            
            return result;
        }

        private bool HandleMouseMove(MouseEventArgs e)
        {
            PointF pt = new PointF(e.X, e.Y);

            if (!_showPeptideTotals)
            {
                if (_peakBoundDragInfos != null && _peakBoundDragInfos.Length > 0)
                {
                    graphControl.Cursor = Cursors.VSplit;
                    DoDrag(_peakBoundDragInfos.First().GraphPane, pt);
                    return true;
                }

                if (e.Button != MouseButtons.None)
                    return false;
            }

            bool doFullScanTracking = _enableTrackingDot;

            using (Graphics g = CreateGraphics())
            {
                object nearest;
                int index;
                GraphPane nearestGraphPane;
                if (FindNearestObject(pt, g, out nearestGraphPane, out nearest, out index))
                {
                    var label = nearest as TextObj;
                    if (label != null)
                    {
                        doFullScanTracking = false;
                        TransitionGroupDocNode nodeGroup;
                        TransitionDocNode nodeTran;
                        if (_showPeptideTotals ||
                            (!_extractor.HasValue && !FindAnnotatedPeakRetentionTime(label, out nodeGroup, out nodeTran).IsZero) ||
                            !FindAnnotatedSpectrumRetentionTime(label).IsZero)
                        {
                            graphControl.Cursor = Cursors.Hand;
                            return true;
                        }
                    }

                    if (_showPeptideTotals)
                        return false;

                    var line = nearest as LineObj;
                    if (line != null)
                    {
                        ChromGraphItem graphItem;
                        if (!FindAnnotatedSpectrumRetentionTime(line).IsZero)
                        {
                            graphControl.Cursor = Cursors.Hand;
                            return true;
                        }
                        GraphPane graphPane;
                        if (!_extractor.HasValue && (!FindBestPeakBoundary(pt, out graphPane, out graphItem).IsZero || graphPane != nearestGraphPane))
                        {
                            graphControl.Cursor = Cursors.VSplit;
                            return true;
                        }
                    }

                    if (_extractor.HasValue)
                        return false;

                    if (nearest is XAxis && IsGroupActive)
                    {
                        graphControl.Cursor = Cursors.VSplit;
                        return true;
                    }

                }
            }

            // Show full scan tracking dot.
            ShowHighlightPoint(pt, doFullScanTracking);
            if (IsOverHighlightPoint(pt))
            {
                graphControl.Cursor = Cursors.Hand;
                return true;
            }

            return false;
        }


        private void graphControl_MouseLeaveEvent(object sender, EventArgs e)
        {
            HideFullScanTrackingPoint();
            Refresh();
        }

        private void HideFullScanTrackingPoint()
        {
            _showingTrackingDot = false;
            if (!_enableTrackingDot)
                return;
            foreach (var graphPane in GraphPanes)
            {
                if (graphPane.CurveList.Count > FULLSCAN_TRACKING_INDEX)
                    graphPane.CurveList[FULLSCAN_TRACKING_INDEX].IsVisible = false;
            }
        }

        private bool IsOverHighlightPoint(PointF pt)
        {
            if (!_enableTrackingDot)
                return false;
            var graphPane = GraphPaneFromPoint(pt) as MSGraphPane;
            return graphPane != null && _showingTrackingDot;
        }

        private CurveItem _closestCurve;

        /// <summary>
        /// Display the closest curve point to the cursor.
        /// </summary>
        /// <param name="pt">Cursor coordinates</param>
        /// <param name="showPoint">True to display tracking point.</param>
        private void ShowHighlightPoint(PointF pt, bool showPoint)
        {
            var graphPane = GraphPaneFromPoint(pt) as MSGraphPane;

            if (graphPane != null && showPoint)
            {
                // Find the closest curve point to the cursor.
                graphPane.FindClosestCurve(GetCurves(graphPane), pt, 20, out _closestCurve, out _fullScanTrackingPointLocation);

                // Display the highlight point.
                if (_closestCurve != null && graphPane.CurveList.Count > FULLSCAN_TRACKING_INDEX)
                {
                    double x, y;
                    graphPane.ReverseTransform(_fullScanTrackingPointLocation, out x, out y);
                    var lineItem = (LineItem)graphPane.CurveList[FULLSCAN_TRACKING_INDEX];
                    lineItem[0].X = x;
                    lineItem[0].Y = y;
                    lineItem.Symbol.Fill.Color = Color.FromArgb(150, _closestCurve.Color);
                    lineItem.Symbol.Border.Color = Color.FromArgb(
                        (int)(_closestCurve.Color.R * 0.6),
                        (int)(_closestCurve.Color.G * 0.6),
                        (int)(_closestCurve.Color.B * 0.6));
                    lineItem.IsVisible = true;
                    _showingTrackingDot = true;

                    graphControl.Invalidate(graphControl.Region);
                    graphControl.Update();
                }
            }
        }

        private bool graphControl_MouseDownEvent(ZedGraphControl sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                PointF pt = new PointF(e.X, e.Y);
                if (!_showPeptideTotals && IsOverHighlightPoint(pt))
                {
                    var graphPane = GraphPaneFromPoint(pt);
                    if (graphPane != null)
                    {
                        // Uncomment to get coordinates for tests
//                        double xScaled, yScaled;
//                        graphPane.ReverseTransform(pt, out xScaled, out yScaled);
//                        Console.WriteLine(@"Clicked x={0}, y={1}", xScaled, yScaled); 

                        FireClickedChromatogram(graphPane);
                    }
                    return true;
                }

                using (Graphics g = CreateGraphics())
                {
                    GraphPane nearestGraphPane;
                    object nearest;
                    int index;

                    if (FindNearestObject(pt, g, out nearestGraphPane, out nearest, out index))
                    {
                        var label = nearest as TextObj;
                        if (label != null)
                        {
                            // Select corresponding peptide.
                            if (_showPeptideTotals)
                            {
                                var idPath = FindIdentityPath(label);
                                if (idPath != null)
                                {
                                    _stateProvider.SelectPath(idPath.Parent);
                                    UpdateUI();
                                }
                                return true;
                            }

                            if (!_extractor.HasValue)
                            {
                                TransitionGroupDocNode nodeGroup;
                                TransitionDocNode nodeTran;
                                var peakTime = FindAnnotatedPeakRetentionTime(label, out nodeGroup, out nodeTran);
                                if (!peakTime.IsZero)
                                {
                                    FirePickedPeak(nodeGroup, nodeTran, peakTime);
                                    graphControl.Cursor = Cursors.Hand; // ZedGraph changes to crosshair without this
                                    return true;
                                }
                            }
                            var spectrumTime = FindAnnotatedSpectrumRetentionTime(label);
                            if (!spectrumTime.IsZero)
                            {
                                FirePickedSpectrum(spectrumTime);
                                return true;
                            }
                        }

                        if (_showPeptideTotals)
                            return false;

                        var line = nearest as LineObj;
                        if (line != null)
                        {
                            var spectrumTime = FindAnnotatedSpectrumRetentionTime(line);
                            if (!spectrumTime.IsZero)
                            {
                                FirePickedSpectrum(spectrumTime);
                                return true;
                            }
                            if (!_extractor.HasValue)
                            {
                                ChromGraphItem graphItem;
                                GraphPane graphPane;
                                ScaledRetentionTime time = FindBestPeakBoundary(pt, out graphPane, out graphItem);
                                if (!time.IsZero)
                                {
                                    _peakBoundDragInfos = new[] { StartDrag(graphPane, graphItem, pt, time, false) };
                                    graphControl.Cursor = Cursors.VSplit; // ZedGraph changes to crosshair without this
                                    return true;
                                }
                            }
                        }

                        if (_extractor.HasValue)
                            return false;

                        if (nearest is XAxis && IsGroupActive)
                        {
                            CurveItem[] changeCurves;
                            if (IsMultiGroup)
                            {
                                changeCurves = GetCurves(nearestGraphPane).ToArray();
                            }
                            else
                            {
                                var firstCurve = GetCurves(nearestGraphPane).FirstOrDefault();
                                if (null == firstCurve)
                                {
                                    changeCurves = new CurveItem[0];
                                }
                                else
                                {
                                    changeCurves = new[] {firstCurve};
                                }
                            }
                            var listDragInfos = new List<PeakBoundsDragInfo>();
                            foreach (var curveItem in changeCurves)
                            {
                                ChromGraphItem graphItem;
                                var time = FindBestPeakTime(nearestGraphPane, curveItem, pt, out graphItem);
                                if (!time.IsZero)
                                    listDragInfos.Add(StartDrag(nearestGraphPane, graphItem, pt, time, true));
                            }
                            _peakBoundDragInfos = listDragInfos.ToArray();
                            graphControl.Cursor = Cursors.VSplit; // ZedGraph changes to crosshair without this
                            return true;
                        }
                    }
                }
            }
            else if (_peakBoundDragInfos != null)
            {
                // Wait and block mouse up event from showing the menu
                // EndDrag();
                return true;
            }

            return false;
        }

        private bool graphControl_MouseUpEvent(ZedGraphControl sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                PointF pt = new PointF(e.X, e.Y);
                if (_peakBoundDragInfos != null && _peakBoundDragInfos.Length > 0)
                {
                    DoDrag(_peakBoundDragInfos.First().GraphPane, pt);
                    EndDrag(true);
                    return true;
                }
            }
            else if (_peakBoundDragInfos != null)
            {
                EndDrag(false);
                return true;
            }
            return false;
        }

        private PeakBoundsDragInfo StartDrag(GraphPane graphPane, ChromGraphItem graphItem, PointF pt,
            ScaledRetentionTime time, bool bothBoundaries)
        {
            var tranPeakInfo = graphItem.TransitionChromInfo;
            var startTime = time;
            var endTime = time;
            if (tranPeakInfo == null)
                bothBoundaries = true;
            else
            {
                startTime = graphItem.ScaleRetentionTime(tranPeakInfo.StartRetentionTime);
                endTime = graphItem.ScaleRetentionTime(tranPeakInfo.EndRetentionTime);
            }
            bool draggingEnd = Math.Abs(time.DisplayTime - startTime.DisplayTime) > Math.Abs(time.DisplayTime - endTime.DisplayTime);
            ScaledRetentionTime anchorTime, caretTime;
            if (bothBoundaries)
                anchorTime = caretTime = time;
            else if (draggingEnd)
            {
                anchorTime = startTime;
                caretTime = endTime;
            }
            else
            {
                anchorTime = endTime;
                caretTime = startTime;
            }
            var retentionMsMs = RetentionMsMs;
            bool alignedTimes = false;
            if (retentionMsMs == null)
            {
                retentionMsMs = AlignedRetentionMsMs;
                alignedTimes = true;
            }
            var dragType = (draggingEnd ? PeakBoundsChangeType.end : PeakBoundsChangeType.start);
            var changeType = bothBoundaries ? PeakBoundsChangeType.both : dragType;
            var peakBoundDragInfo = new PeakBoundsDragInfo(graphPane, graphItem, retentionMsMs, alignedTimes,
                                                           pt, dragType, changeType)
                                        {
                                            AnchorTime = anchorTime,
                                            CaretTime = caretTime
                                        };
            return (graphItem.DragInfo = peakBoundDragInfo);
        }

        public float GetPeakBoundaryTime(ChromGraphItem chromGraphItem, double displayTime)
        {
            double measuredTime = chromGraphItem.TimeRegressionFunction == null
                ? displayTime
                : chromGraphItem.TimeRegressionFunction.GetX(displayTime);
            var chromatogramInfo = chromGraphItem.Chromatogram;
            if (chromatogramInfo.TimeIntervals != null)
            {
                return (float) measuredTime;
            }

            var interpolatedTimeIntensities = chromatogramInfo.GetInterpolatedTimeIntensities();
            int index = interpolatedTimeIntensities.IndexOfNearestTime((float) displayTime);
            if (index < 0 || index >= interpolatedTimeIntensities.Times.Count)
            {
                return 0;
            }

            return interpolatedTimeIntensities.Times[index];
        }

        public bool DoDrag(GraphPane graphPane, PointF pt)
        {
            // Calculate new location of boundary from mouse position
            double time, yTemp;
            graphPane.ReverseTransform(pt, out time, out yTemp);

            bool changed = false;
            foreach (var dragInfo in _peakBoundDragInfos)
            {
                if (dragInfo.MoveTo(pt, time, IsMultiGroup, 
                    (startTime, endTime) => FindMaxPeakItem(graphPane, startTime, endTime)))
                    changed = true;
            }
            return changed;
        }

        private void EndDrag(bool commit)
        {
            if (_peakBoundDragInfos == null)
                return;

            var peakBoundDragInfos = _peakBoundDragInfos;
            _peakBoundDragInfos = null;

            if (commit && peakBoundDragInfos.IndexOf(info => info.Moved) != -1)
                FireChangedPeakBounds(peakBoundDragInfos);

            foreach (var dragInfo in peakBoundDragInfos)
            {
                dragInfo.GraphItemBest.HideBest = false;
                dragInfo.GraphItem.DragInfo = null;
            }
            Refresh();
        }

        private bool FindNearestObject(PointF pt, Graphics g, out GraphPane nearestGraphPane, out object nearestCurve, out int index)
        {
            try
            {
                var graphPane = GraphPaneFromPoint(pt);
                if (null != graphPane)
                {
                    if (graphPane.FindNearestObject(pt, g, out nearestCurve, out index))
                    {
                        nearestGraphPane = graphPane;
                        return true;
                    }
                }
            }
            catch (ExternalException)
            {
                // Apparently GDI+ was throwing this exception on some systems.
                // It was showing up occasionally in the undandled exceptions.
                // Better to silently fail to find anything than to put up the
                // nasty unexpected error form.
            }
            nearestGraphPane = null;
            nearestCurve = null;
            index = -1;
            return false;
        }

        private GraphPane GraphPaneFromPoint(PointF point)
        {
            if (graphControl.MasterPane.PaneList.Count == 1)
            {
                return graphControl.MasterPane.PaneList[0];
            }
            else
            {
                return graphControl.MasterPane.FindPane(point);
            }
        }

        #endregion

        private void graphControl_ContextMenuBuilder(ZedGraphControl sender,
                                                     ContextMenuStrip menuStrip, Point mousePt,
                                                     ZedGraphControl.ContextMenuObjectState objState)
        {
            var paneKey = _graphHelper.GetPaneKey(GraphPaneFromPoint(mousePt));
            _stateProvider.BuildChromatogramMenu(sender, paneKey, menuStrip, GetChromFileInfoId());
        }

        protected override void OnClosed(EventArgs e)
        {
            _documentContainer.UnlistenUI(OnDocumentUIChanged);
        }

        public static IList<Color> COLORS_GROUPS
        {
            get { return ColorScheme.CurrentColorScheme.PrecursorColors; }
        }

        public static IList<Color> COLORS_LIBRARY { get { return ColorScheme.CurrentColorScheme.TransitionColors; }}

        public static int GetColorIndex(TransitionGroupDocNode nodeGroup, int countLabelTypes, ref Adduct charge,
                                        ref int iCharge)
        {
            // Make sure colors stay somewhat consistent among charge states.
            // The same label type should always have the same color, with the
            // first charge state in the peptide matching the peptide label type
            // modification font colors.
            if (!Equals(charge, nodeGroup.TransitionGroup.PrecursorAdduct))
            {
                charge = nodeGroup.TransitionGroup.PrecursorAdduct;
                iCharge++;
            }
            return iCharge * countLabelTypes + nodeGroup.TransitionGroup.LabelType.SortOrder;
        }

        #region Test support

        public void TestMouseMove(double x, double y, PaneKey? paneKey)
        {
            var mouse = TransformCoordinates(x, y, paneKey);
            HandleMouseMove(new MouseEventArgs(MouseButtons.None, 0, (int)mouse.X, (int)mouse.Y, 0));
        }

        public bool IsOverHighlightPoint(double x, double y, PaneKey? paneKey)
        {
            var mouse = TransformCoordinates(x, y, paneKey);
            return IsOverHighlightPoint(mouse);
        }

        public void TestMouseDown(double x, double y, PaneKey? paneKey)
        {
            var mouse = TransformCoordinates(x, y, paneKey);
            graphControl_MouseDownEvent(null, new MouseEventArgs(MouseButtons.Left, 1, (int)mouse.X, (int)mouse.Y, 0));
        }

        public string TestFullScanSelection(double x, double y, PaneKey? paneKey)
        {
            var graphPane = _graphHelper.GetGraphPane(paneKey ?? PaneKey.DEFAULT);
            var selectionDot = graphPane.CurveList[FULLSCAN_SELECTED_INDEX];
            var mouse = TransformCoordinates(x, y, paneKey);
            var dot = TransformCoordinates(selectionDot[0].X, selectionDot[0].Y, paneKey);
            if (!selectionDot.IsVisible)
                return @"selection dot is not visible";
            const int pixelTolerance = 10;
            if (Math.Abs(mouse.X - dot.X) > pixelTolerance || Math.Abs(mouse.Y - dot.Y) > pixelTolerance)
                return $@"mouse coordinates ({x}->{mouse.X}, {y}->{mouse.Y}) and selection dot coordinates ({selectionDot[0].X}->{dot.X}, {selectionDot[0].Y}->{dot.Y}) are too far apart";
            return string.Empty;
        }

        #endregion Test support
    }

    public static class AddTransitions
    {
        /// <summary>
        /// Add intensitites of two transitions where they overlap in time.
        /// </summary>
        /// <param name="times1">Times for first transition.</param>
        /// <param name="intensities1">Intensities for first transition.</param>
        /// <param name="times2">Times for second transition.</param>
        /// <param name="intensities2">Intensities for second transition.</param>
        /// <param name="sumTimes">Times for summed transition.</param>
        /// <param name="sumIntensities">Intensities for summed transition.</param>
        /// <returns>True if a sum is returned, false if there was no overlap.</returns>
        public static bool Add(
            float[] times1, float[] intensities1,
            float[] times2, float[] intensities2,
            out float[] sumTimes, out float[] sumIntensities)
        {
            sumTimes = null;
            sumIntensities = null;

            if (times1.Length == 0 || times2.Length == 0)
                return false;   // nothing to add

            // We need to sum the *intersection* of these two transitions.  To make that
            // easier, we arrange for the one with the earliest start time to be in times1/intensities1.
            if (times1[0] > times2[0])
            {
                Helpers.Swap(ref times1, ref times2);
                Helpers.Swap(ref intensities1, ref intensities2);
            }

            float maxTime1 = times1[times1.Length - 1];
            if (times2[0] > maxTime1)
                return false;   // no intersection

            // No interpolation needed for single point.
            if (times1.Length == 1)
            {
                if (times1[0].Equals(times2[0]))
                {
                    sumTimes = new[] {times1[0]};
                    sumIntensities = new[] {intensities1[0] + intensities2[0]};
                    return true;
                }
                return false;
            }

            // Count number of points to sum.
            int count = times2.Length - 1;
            while (times2[count] > maxTime1)
                count--;
            count++;

            // Now add the intensities within the intersection.
            // Currently, we select the transition with the greater starting time to
            // determine sampling.  A better choice might be the transition with the
            // higher sampling rate, but that is more difficult to write.
            sumTimes = new float[count];
            sumIntensities = new float[count];
            float interval1 = times1[1] - times1[0];
            int index1 = 1;
            for (int i = 0; i < count; i++)
            {
                // Select the next point to interpolate from in the times1 array.
                while (index1 < times1.Length && times1[index1] <= times2[i])
                    index1++;
                sumTimes[i] = times2[i];
                double interp = (times2[i] - times1[index1-1]) / interval1;
                sumIntensities[i] = (float)
                    (intensities2[i] + (1.0 - interp) * intensities1[index1 - 1] + 
                    ((index1 < intensities1.Length) ? interp * intensities1[index1] : 0));
            }

            return true;
        }
    }

    internal sealed class PeakBoundsDragInfo
    {
        public PeakBoundsDragInfo(GraphPane graphPane, ChromGraphItem graphItem, double[] retentionTimesMsMs, bool alignedTimes,
                                  PointF startPoint, PeakBoundsChangeType dragType, PeakBoundsChangeType changeType)
        {
            GraphPane = graphPane;
            GraphItem = GraphItemBest = graphItem;
            GraphItemBest.HideBest = true;
            RetentionTimesMsMs = retentionTimesMsMs;
            IsAlignedTimes = alignedTimes;
            StartPoint = startPoint;
            DragType = dragType;
            ChangeType = changeType;
        }

        public GraphPane GraphPane { get; set; }
        public ChromGraphItem GraphItem { get; set; }
        public ChromGraphItem GraphItemBest { get; private set; }
        public double[] RetentionTimesMsMs { get; private set; }
        public bool IsAlignedTimes { get; private set; }

        public PointF StartPoint { get; private set; }
        public PeakBoundsChangeType DragType { get; private set; }
        public PeakBoundsChangeType ChangeType { get; private set; }
        public bool Moved { get; private set; }

        public ScaledRetentionTime AnchorTime { get; set; }
        public ScaledRetentionTime CaretTime { get; set; }

        public ScaledRetentionTime StartTime
        {
            get { return AnchorTime.MeasuredTime < CaretTime.MeasuredTime ?  AnchorTime : CaretTime; }
        }

        public ScaledRetentionTime EndTime
        {
            get { return AnchorTime.MeasuredTime < CaretTime.MeasuredTime ? CaretTime : AnchorTime; }
        }

        public bool IsIdentified
        {
            get
            {
                if (RetentionTimesMsMs == null)
                    return false;

                double startTime = StartTime.MeasuredTime;
                double endTime = EndTime.MeasuredTime;
                return RetentionTimesMsMs.Any(time => startTime <= time && time <= endTime);
            }
        }

        // Must move a certain number of pixels to count as having moved
        private const int MOVE_THRESHOLD = 3;

        public bool MoveTo(PointF pt, double time, bool multiGroup,
                           Func<ScaledRetentionTime, ScaledRetentionTime, ChromGraphItem> findMaxPeakItem)
        {
            // Make sure the mouse moves a minimum distance before starting
            // the drag.
            if (!Moved && Math.Abs(pt.X - StartPoint.X) < MOVE_THRESHOLD &&
                Math.Abs(pt.Y - StartPoint.Y) < MOVE_THRESHOLD)
            {
                return false;
            }
            Moved = true;

            var rtNew = GraphItem.GetValidPeakBoundaryTime(time);
            if (!rtNew.Equals(CaretTime))
            {
                CaretTime = rtNew;
                // If editing a single group, look for the maximum peak of the transitions
                // within the current range, and set the drag-info on that graph item.
                if (!multiGroup)
                {
                    var graphItemMax = findMaxPeakItem(StartTime, EndTime);
                    if (graphItemMax != null && graphItemMax != GraphItem)
                    {
                        if (GraphItem != null) // ReSharper
                            GraphItem.DragInfo = null;
                        GraphItem = graphItemMax;

                    }
                }
                GraphItem.DragInfo = this;
                return true;
            }
            return false;
        }
    }

    public enum PeakBoundsChangeType
    {
        start,
        end,
        both
    }

    public abstract class PeakEventArgs : EventArgs
    {
        protected PeakEventArgs(IdentityPath groupPath, string nameSet, MsDataFileUri filePath)
        {
            GroupPath = groupPath;
            NameSet = nameSet;
            FilePath = filePath;
        }

        public IdentityPath GroupPath { get; private set; }
        public string NameSet { get; private set; }
        public MsDataFileUri FilePath { get; private set; }
    }

    public sealed class PickedPeakEventArgs : PeakEventArgs
    {
        public PickedPeakEventArgs(IdentityPath groupPath, Identity transitionId,
                                   string nameSet, MsDataFileUri filePath, ScaledRetentionTime retentionTime)
            : base(groupPath, nameSet, filePath)
        {
            TransitionId = transitionId;
            RetentionTime = retentionTime;
        }

        public Identity TransitionId { get; private set; }
        public ScaledRetentionTime RetentionTime { get; private set; }
    }

    public sealed class ClickedChromatogramEventArgs : EventArgs
    {
        public ClickedChromatogramEventArgs(IScanProvider scanProvider, int transitionIndex, int scanIndex)
        {
            ScanProvider = scanProvider;
            TransitionIndex = transitionIndex;
            ScanIndex = scanIndex;
        }

        public IScanProvider ScanProvider { get; private set; }
        public int TransitionIndex { get; private set; }
        public int ScanIndex { get; private set; }
    }

    public sealed class ChangedPeakBoundsEventArgs : PeakEventArgs
    {
        public ChangedPeakBoundsEventArgs(IdentityPath groupPath,
                                          Transition transition,
                                          string nameSet,
                                          MsDataFileUri filePath,
                                          ScaledRetentionTime startTime,
                                          ScaledRetentionTime endTime,
                                          PeakIdentification? identified,
                                          PeakBoundsChangeType changeType,
                                          bool syncGeneratedChange = false)
            : base(groupPath, nameSet, filePath)
        {
            Transition = transition;
            StartTime = startTime;
            EndTime = endTime;
            Identified = identified;
            ChangeType = changeType;
            SyncGeneratedChange = syncGeneratedChange;
        }

        public Transition Transition { get; private set; }
        public ScaledRetentionTime StartTime { get; private set; }
        public ScaledRetentionTime EndTime { get; private set; }
        public PeakIdentification? Identified { get; private set; }
        public bool IsIdentified { get { return Identified.HasValue && Identified != PeakIdentification.FALSE; } }
        public PeakBoundsChangeType ChangeType { get; private set; }
        public bool SyncGeneratedChange { get; }
    }

    public sealed class ChangedMultiPeakBoundsEventArgs : EventArgs
    {
        public ChangedMultiPeakBoundsEventArgs(ChangedPeakBoundsEventArgs[] changes)
        {
            Changes = changes;
        }

        public ChangedPeakBoundsEventArgs[] Changes { get; private set; }
    }

    public sealed class PickedSpectrumEventArgs : EventArgs
    {
        public PickedSpectrumEventArgs(SpectrumIdentifier spectrumId)
        {
            SpectrumId = spectrumId;
        }

        public SpectrumIdentifier SpectrumId { get; private set; }
    }

    public sealed class ZoomEventArgs : EventArgs
    {
        public ZoomEventArgs(ZoomState zoomState)
        {
            ZoomState = zoomState;
        }

        public ZoomState ZoomState { get; private set; }
    }

    public struct ScaledRetentionTime
    {
        public static readonly ScaledRetentionTime ZERO = default(ScaledRetentionTime);
        public ScaledRetentionTime(double measuredTime) : this(measuredTime, measuredTime)
        {
        }
        public ScaledRetentionTime(double measuredTime, double scaledTime) : this()
        {
            MeasuredTime = measuredTime;
            DisplayTime = scaledTime;
        }
        public bool IsZero
        {
            get { return Equals(ZERO); }
        }
        public double MeasuredTime { get; private set; }
        public double DisplayTime { get; private set; }
        public override string ToString()
        {
            if (DisplayTime == MeasuredTime)
            {
                return MeasuredTime.ToString(CultureInfo.InvariantCulture);
            }
            return string.Format(@"{0} ({1})", MeasuredTime, DisplayTime);
        }
    }

    public struct RawTimesInfoItem
    {
        public double StartBound { get; set; }
        public double EndBound { get; set; }
    }
}
