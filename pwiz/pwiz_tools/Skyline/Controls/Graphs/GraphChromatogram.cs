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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using pwiz.MSGraph;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using ZedGraph;

namespace pwiz.Skyline.Controls.Graphs
{
    public enum ShowRTChrom { none, all, best, threshold }

    public enum AutoZoomChrom { none, peak, window, both }

    public enum DisplayTypeChrom { single, precursors, products, all, total, base_peak, tic }

    public partial class GraphChromatogram : DockableFormEx, IGraphContainer
    {
        public const double DEFAULT_PEAK_RELATIVE_WINDOW = 3.4;
        private readonly GraphHelper _graphHelper;

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
                return Helpers.ParseEnum(Settings.Default.TransformTypeChromatogram, TransformChrom.none);
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
            if (displayType == DisplayTypeChrom.base_peak || displayType == DisplayTypeChrom.tic)
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

            SpectrumDisplayInfo SelectedSpectrum { get; }
            GraphValues.IRetentionTimeTransformOp GetRetentionTimeTransformOperation();

            void BuildChromatogramMenu(ZedGraphControl zedGraphControl, ContextMenuStrip menuStrip, ChromFileInfoId chromFileInfoId);
        }

        private class DefaultStateProvider : IStateProvider
        {
            public TreeNodeMS SelectedNode { get { return null; } }
            public SpectrumDisplayInfo SelectedSpectrum { get { return null; } }
            public void BuildChromatogramMenu(ZedGraphControl zedGraphControl, ContextMenuStrip menuStrip, ChromFileInfoId chromFileInfoId) { }
            public GraphValues.IRetentionTimeTransformOp GetRetentionTimeTransformOperation()
            {
                return null;
            }
        }

        private string _nameChromatogramSet;
        private readonly IDocumentUIContainer _documentContainer;
        private readonly IStateProvider _stateProvider;

        // Active graph state
        private ChromExtractor? _extractor;
        private TransitionGroupDocNode[] _nodeGroups;
        private IdentityPath[] _groupPaths;
        private ChromatogramGroupInfo[][] _arrayChromInfo;
        private int _chromIndex;

        public GraphChromatogram(string name, IDocumentUIContainer documentUIContainer)
        {
            InitializeComponent();
            _graphHelper = GraphHelper.Attach(graphControl);
            NameSet = name;
            Icon = Resources.SkylineData;

            _nameChromatogramSet = name;
            _documentContainer = documentUIContainer;
            _documentContainer.ListenUI(OnDocumentUIChanged);
            _stateProvider = documentUIContainer as IStateProvider ??
                             new DefaultStateProvider();
            
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

        public int CurveCount { get { return GraphPanes.Sum(pane=>pane.CurveList.Count); } }

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
                string filePath = FilePath;
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
                string filePath = FilePath;
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

        private void graphControl_ZoomEvent(ZedGraphControl sender, ZoomState oldState, ZoomState newState)
        {
            if (Settings.Default.AutoZoomAllChromatograms && ZoomAll != null)
                ZoomAll.Invoke(this, new ZoomEventArgs(newState));
        }

        public void ZoomTo(ZoomState zoomState)
        {
            zoomState.ApplyState(GraphPanes.First());
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
                            !ReferenceEquals(nodeTran.Results[_chromIndex], nodeTranCurrent.Results[_chromIndex]))
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

                return _arrayChromInfo[iSelected];
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
            get
            {
                var graphItem = (ChromGraphItem) graphControl.GraphPane.CurveList.Last().Tag;
                return graphItem.SelectedRetentionMsMs;
            }
        }

        public double? PredictedRT
        {
            get
            {
                var graphItem = (ChromGraphItem) graphControl.GraphPane.CurveList.Last().Tag;
                return graphItem.RetentionPrediction;
            }
        }

        public double? BestPeakTime
        {
            get
            {
                var graphItem = graphControl.GraphPane.CurveList.Select(c => c.Tag).Cast<ChromGraphItem>()
                    .First(g => g.BestPeakTime > 0);
                return graphItem.BestPeakTime;
            }
        }

        /// <summary>
        /// Returns the file path for the selected file of the groups.
        /// </summary>
        private string FilePath
        {
            get
            {
                var chromGroupInfos = ChromGroupInfos;
                int i = chromGroupInfos.IndexOf(info => info != null);

                return (i != -1 ? chromGroupInfos[i].FilePath : null);
            }
        }

        private void comboFiles_SelectedIndexChanged(object sender, EventArgs e)
        {
            // If the selected file is changing, then all of the chromatogram data
            // on display will change, and the graph should be auto-zoomed, if
            // any auto-zooming is turned on.
            UpdateUI(true);
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

        public void UpdateUI()
        {
            UpdateUI(false);
        }

        private void UpdateUI(bool forceZoom)
        {
            IsCacheInvalidated = false;

            // Only worry about updates, if the graph is visible
            // And make sure it is not disposed, since rendering happens on a timer
            if (!Visible || IsDisposed)
                return;

            var settings = DocumentUI.Settings;
            var results = settings.MeasuredResults;
            if (results == null)
                return;
            ChromatogramSet chromatograms;
            if (!results.TryGetChromatogramSet(_nameChromatogramSet, out chromatograms, out _chromIndex))
                return;

            string xAxisTitle = GraphValues.ToLocalizedString(RTPeptideValue.Retention);
            IRegressionFunction timeRegressionFunction = null;
            var retentionTimeTransformOp = _stateProvider.GetRetentionTimeTransformOperation();
            if (null != retentionTimeTransformOp && null != _arrayChromInfo)
            {
                retentionTimeTransformOp.TryGetRegressionFunction(chromatograms.FindFile(ChromGroupInfos[0]), out timeRegressionFunction);
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

            PeptideDocNode nodePep = null;
            ExplicitMods mods = null;
            TransitionGroupDocNode[] nodeGroups = null;
            IdentityPath[] groupPaths = null;
            PeptideTreeNode nodePepTree;
            if (nodeGroupTree != null)
            {
                nodePepTree = nodeGroupTree.Parent as PeptideTreeNode;
                nodeGroups = new[] {nodeGroupTree.DocNode};
                groupPaths = new[] {nodeGroupTree.Path};
            }
            else
            {
                nodePepTree = nodeTree as PeptideTreeNode;
                if (nodePepTree != null && nodePepTree.ChildDocNodes.Count > 0)
                {
                    var children = nodePepTree.ChildDocNodes;
                    nodeGroups = new TransitionGroupDocNode[children.Count];
                    groupPaths = new IdentityPath[children.Count];
                    var pathParent = nodePepTree.Path;
                    for (int i = 0; i < nodeGroups.Length; i++)
                    {
                        var nodeGroup = (TransitionGroupDocNode) children[i];
                        nodeGroups[i] = nodeGroup;
                        groupPaths[i] = new IdentityPath(pathParent, nodeGroup.Id);
                    }
                }
            }
            if (nodePepTree != null)
            {
                nodePep = nodePepTree.DocNode;
                mods = nodePep.ExplicitMods;
            }

            // Clear existing data from the graph pane
            _graphHelper.ResetForChromatograms(nodeGroups == null ? null : nodeGroups.Select(node=>node.TransitionGroup));

            double bestStartTime = double.MaxValue;
            double bestEndTime = 0;

            // Check for appropriate chromatograms to load
            bool changedGroups = false;

            try
            {
                // Make sure all the chromatogram info for the relevant transition groups is present.
                float mzMatchTolerance = (float) settings.TransitionSettings.Instrument.MzMatchTolerance;
                var displayType = GetDisplayType(DocumentUI);
                bool changedGroupIds;
                if (displayType == DisplayTypeChrom.base_peak || displayType == DisplayTypeChrom.tic)
                {
                    var extractor = displayType == DisplayTypeChrom.base_peak
                                        ? ChromExtractor.base_peak
                                        : ChromExtractor.summed;
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
                                              ref bestStartTime, ref bestEndTime);

                        if (nodeGroups != null)
                        {
                            foreach (var chromGraphItem in _graphHelper.ListPrimaryGraphItems())
                            {
                                SetRetentionTimeIdIndicators(chromGraphItem.Value, settings, nodeGroups, mods);
                            }
                        }
                    }
                }
                else if (nodeGroups != null && EnsureChromInfo(results,
                                                               chromatograms,
                                                               nodePep,
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
                        var nodeGroupGraphPaneKeys = nodeGroups.Select(nodeGroup => new GraphHelper.PaneKey(nodeGroup)).ToArray();
                        var countDistinctGraphPaneKeys = nodeGroupGraphPaneKeys.Distinct().Count();
                        multipleGroupsPerPane = countDistinctGraphPaneKeys != nodeGroupGraphPaneKeys.Length;
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
                                      countLabelTypes, ref bestStartTime, ref bestEndTime);
                    }
                        // Single group with optimization data, not a transition selected,
                        // and single display mode
                    else if (chromatograms.OptimizationFunction != null &&
                             nodeTranTree == null && IsSingleTransitionDisplay)
                    {
                        DisplayOptimizationTotals(timeRegressionFunction, chromatograms, mzMatchTolerance,
                                                  ref bestStartTime, ref bestEndTime);
                    }
                    else
                    {
                        var nodeTranSelected = (nodeTranTree != null ? nodeTranTree.DocNode : null);
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
                                                   new GraphHelper.PaneKey(nodeGroup),
                                                   DisplayType, ref bestStartTime, ref bestEndTime);
                            }
                            else
                            {
                                displayType = GetDisplayType(DocumentUI, nodeGroup);
                                if (displayType != DisplayTypeChrom.products)
                                {
                                    DisplayTransitions(timeRegressionFunction, nodeTranSelected, chromatograms, mzMatchTolerance,
                                                       nodeGroup, chromGroupInfo, GraphHelper.PaneKey.PRECURSORS, DisplayTypeChrom.precursors,
                                                       ref bestStartTime, ref bestEndTime);
                                }
                                if (displayType != DisplayTypeChrom.precursors)
                                {
                                    DisplayTransitions(timeRegressionFunction, nodeTranSelected, chromatograms, mzMatchTolerance, 
                                                       nodeGroup, chromGroupInfo, GraphHelper.PaneKey.PRODUCTS, DisplayTypeChrom.products,
                                                       ref bestStartTime, ref bestEndTime);
                                }
                            }
                        }
                    }

                    foreach (var chromGraphItem in _graphHelper.ListPrimaryGraphItems())
                    {
                        SetRetentionTimeIndicators(chromGraphItem.Value, settings, chromatograms,
                                                    nodeGroups, mods);
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

            // Show unavailable message, if no chromatogoram loaded
            if (!_graphHelper.ListPrimaryGraphItems().Any())
            {
                if (nodeGroups == null || changedGroups)
                {
                    UpdateToolbar(null);
                    EndDrag(false);
                }
                if (CurveCount == 0)
                {
                    string message = null;
                    if (nodePep == null)
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
                    }
                    SetGraphItem(new UnavailableChromGraphItem(message));
                }
            }
            else 
            {
                _graphHelper.FinishedAddingChromatograms(bestStartTime, bestEndTime, forceZoom);
            }

            foreach (var graphPane in GraphPanes)
            {
                graphPane.XAxis.Title.Text = xAxisTitle;
            }
 
            graphControl.IsEnableVPan = graphControl.IsEnableVZoom =
                                        !Settings.Default.LockYChrom;
            graphControl.Refresh();
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

        private void DisplayAllIonsSummary(IRegressionFunction timeRegressionFunction,
                                           TransitionDocNode nodeTranSelected,
                                           ChromatogramSet chromatograms,
                                           ChromExtractor extractor,
                                           ref double bestStartTime,
                                           ref double bestEndTime)
        {

            var chromGroupInfo = ChromGroupInfos[0];
            var info = chromGroupInfo.GetTransitionInfo(0, 0);
            var fileId = chromatograms.FindFile(chromGroupInfo);

            var nodeGroup = _nodeGroups != null ? _nodeGroups[0] : null;
            if (nodeGroup == null)
                nodeTranSelected = null;

            TransitionChromInfo tranPeakInfo = null;
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

                    // Adjust best peak window used to zoom the graph to the best peak
                    AddBestPeakTimes(transitionChromInfo, ref bestStartTime, ref bestEndTime);
                }
            }

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
                                               0,
                                               COLORS_GROUPS[(int)extractor],
                                               FontSize,
                                               LineWidth);
            _graphHelper.AddChromatogram(new GraphHelper.PaneKey(nodeGroup), graphItem);
        }

        private void SetGraphItem(IMSGraphItemInfo graphItem)
        {
            _graphHelper.ResetForChromatograms(null);
            _graphHelper.SetErrorGraphItem(graphItem);
        }

        private void DisplayTransitions(IRegressionFunction timeRegressionFunction,
                                        TransitionDocNode nodeTranSelected,
                                        ChromatogramSet chromatograms,
                                        float mzMatchTolerance,
                                        TransitionGroupDocNode nodeGroup,
                                        ChromatogramGroupInfo chromGroupInfo,
                                        GraphHelper.PaneKey graphPaneKey,
                                        DisplayTypeChrom displayType,
                                        ref double bestStartTime,
                                        ref double bestEndTime)
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
                    arrayChromInfo = chromGroupInfo.GetAllTransitionInfo((float) nodeTranSelected.Mz,
                                                                            mzMatchTolerance,
                                                                            chromatograms.OptimizationFunction);

                    if (chromatograms.OptimizationFunction != null)
                    {
                        // Make sure the number of steps matches what will show up in the summary
                        // graphs, or the colors won't match up.
                        int numStepsExpected = chromatograms.OptimizationFunction.StepCount*2 + 1;
                        if (arrayChromInfo.Length != numStepsExpected)
                        {
                            arrayChromInfo = ResizeArrayChromInfo(arrayChromInfo, numStepsExpected);
                            allowEmpty = true;
                        }
                    }

                    numTrans = arrayChromInfo.Length;
                    displayTrans = new TransitionDocNode[numTrans];
                    for (int i = 0; i < numTrans; i++)
                        displayTrans[i] = nodeTranSelected;
                }
                numSteps = numTrans/2;
            }
            else
            {
                arrayChromInfo = new ChromatogramInfo[numTrans];
                for (int i = 0; i < numTrans; i++)
                {
                    var nodeTran = displayTrans[i];
                    // Get chromatogram info for this transition
                    arrayChromInfo[i] = chromGroupInfo.GetTransitionInfo((float) nodeTran.Mz, mzMatchTolerance);
                }
            }
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
            for (int i = 0; i < numTrans; i++)
            {
                var nodeTran = displayTrans[i];

                // Store library intensities for dot-product
                if (expectedIntensities != null)
                {
                    if (isFullScanMs)
                        expectedIntensities[i] = nodeTran.HasDistInfo ? nodeTran.IsotopeDistInfo.Proportion : 0;
                    else
                        expectedIntensities[i] = nodeTran.HasLibInfo ? nodeTran.LibInfo.Intensity : 0;
                }

                var info = arrayChromInfo[i];
                if (info == null)
                    continue;

                // Apply any active transform
                info.Transform(transform);

                int step = (numSteps > 0 ? i - numSteps : 0);
                var transitionChromInfo = GetTransitionChromInfo(nodeTran, _chromIndex, fileId, step);

                for (int j = 0; j < numPeaks; j++)
                {
                    var peak = info.GetPeak(j);
                    // If the user clicks on a peak, all of the forced-integration peaks will be activated
//                    if (peak.IsForcedIntegration && !DocumentUI.Settings.TransitionSettings.Integration.IsIntegrateAll)
//                        continue;

                    // Exclude any peaks between the boundaries of the chosen peak.
                    if (IntersectPeaks(peak, transitionChromInfo))
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

                if (transitionChromInfo == null)
                    continue;
                if (maxPeakHeight < transitionChromInfo.Height)
                {
                    maxPeakHeight = transitionChromInfo.Height;
                    bestPeakTran = i;
                    tranPeakInfo = transitionChromInfo;
                }
                AddBestPeakTimes(transitionChromInfo, ref bestStartTime, ref bestEndTime);
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
                (nodeDisplayType == DisplayTypeChrom.single && chromatograms.OptimizationFunction == null)))
            {
                colorOffset = GetDisplayTransitions(nodeGroup, DisplayTypeChrom.precursors).Count();
            }

            for (int i = 0; i < numTrans; i++)
            {
                var info = arrayChromInfo[i];
                if (info == null && !allowEmpty)
                    continue;

                var nodeTran = displayTrans[i];
                int step = numSteps != 0 ? i - numSteps : 0;

                Color color;
                int width = lineWidth;
                if ((numSteps == 0 && ReferenceEquals(nodeTran, nodeTranSelected) ||
                        (numSteps > 0 && step == 0)))
                {
                    color = ChromGraphItem.ColorSelected;
                    width++;
                }
                else
                {
                    color = COLORS_LIBRARY[(iColor + colorOffset) % COLORS_LIBRARY.Length];
                }

                TransitionChromInfo tranPeakInfoGraph = null;
                if (bestPeakTran == i)
                    tranPeakInfoGraph = tranPeakInfo;

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
                                                    step,
                                                    color,
                                                    fontSize,
                                                    width);
                _graphHelper.AddChromatogram(graphPaneKey, graphItem);
                iColor++;
            }
        }

        private void DisplayOptimizationTotals(IRegressionFunction timeRegressionFunction,
                                               ChromatogramSet chromatograms,
                                               float mzMatchTolerance,
                                               ref double bestStartTime,
                                               ref double bestEndTime)
        {
            // Construct and add graph items for all relevant transition groups.
            float fontSize = FontSize;
            int lineWidth = LineWidth;
            int iColor = 0;

            // Get the one and only group
            var nodeGroup = _nodeGroups[0];
            var chromGroupInfo = ChromGroupInfos[0];
            ChromFileInfoId fileId = chromatograms.FindFile(chromGroupInfo);

            int numPeaks = chromGroupInfo.NumPeaks;

            // Collect the chromatogram info for the transition children
            // of this transition group.
            var listChromInfoSets = new List<ChromatogramInfo[]>();
            var listTranisitionChromInfoSets = new List<TransitionChromInfo[]>();
            int totalOptCount = chromatograms.OptimizationFunction.StepCount*2 + 1;
            foreach (TransitionDocNode nodeTran in nodeGroup.Children)
            {
                var infos = chromGroupInfo.GetAllTransitionInfo((float) nodeTran.Mz, mzMatchTolerance,
                                                                chromatograms.OptimizationFunction);
                if (infos.Length == 0)
                    continue;

                // Make sure the total number of chrom info entries match the expected
                // no matter what, so that chromatogram colors will match up with peak
                // area charts.
                if (infos.Length != totalOptCount)
                    infos = ResizeArrayChromInfo(infos, totalOptCount);

                listChromInfoSets.Add(infos);
                var transitionChromInfos = new TransitionChromInfo[totalOptCount];
                int steps = infos.Length/2;
                int offset = totalOptCount/2 - steps;
                for (int i = 0; i < infos.Length; i++)
                {
                    transitionChromInfos[i + offset] = GetTransitionChromInfo(nodeTran, _chromIndex, fileId, i - steps);
                }
                listTranisitionChromInfoSets.Add(transitionChromInfos);
            }

            if (listChromInfoSets.Count == 0 || totalOptCount == 0)
                throw new InvalidDataException(Resources.GraphChromatogram_DisplayOptimizationTotals_No_optimization_data_available);

            // Enumerate optimization steps, grouping the data into graph data by step
            var listGraphData = new List<OptimizationGraphData>();
            for (int i = 0; i < listChromInfoSets.Count; i++)
            {
                var chromInfos = listChromInfoSets[i];
                var transitionChromInfos = listTranisitionChromInfoSets[i];

                for (int j = 0; j < chromInfos.Length; j++)
                {
                    if (listGraphData.Count <= j)
                        listGraphData.Add(new OptimizationGraphData(numPeaks));
                    listGraphData[j].Add(chromInfos[j], transitionChromInfos[j]);
                }
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
                AddBestPeakTimes(graphData.TransitionInfoPrimary, ref bestStartTime, ref bestEndTime);
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
            int totalSteps = totalOptCount/2;
            for (int i = 0; i < listGraphData.Count; i++)
            {
                var graphData = listGraphData[i];

                if (graphData.InfoPrimary != null)
                {
                    int step = i - totalSteps;
                    int width = lineWidth;
                    Color color;
                    if (step == 0)
                    {
                        color = ChromGraphItem.ColorSelected;
                        width++;
                    }
                    else if (nodeGroup.HasLibInfo)
                        color = COLORS_LIBRARY[iColor % COLORS_LIBRARY.Length];
                    else
                        color = COLORS_LIBRARY[iColor % COLORS_LIBRARY.Length];
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
                                                       step,
                                                       color,
                                                       fontSize,
                                                       width);
                    _graphHelper.AddChromatogram(GraphHelper.PaneKey.PRECURSORS, graphItem);
                }

                iColor++;
            }
        }

        private static ChromatogramInfo[] ResizeArrayChromInfo(ChromatogramInfo[] arrayChromInfo, int numStepsExpected)
        {
            int numStepsFound = arrayChromInfo.Length;
            var arrayChromInfoNew = new ChromatogramInfo[numStepsExpected];
            if (numStepsFound < numStepsExpected)
            {
                Array.Copy(arrayChromInfo, 0,
                           arrayChromInfoNew, (numStepsExpected - numStepsFound) / 2, numStepsFound);
            }
            else
            {
                Array.Copy(arrayChromInfo, (numStepsFound - numStepsExpected) / 2,
                           arrayChromInfoNew, 0, numStepsExpected);
            }
            arrayChromInfo = arrayChromInfoNew;
            return arrayChromInfo;
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

        private void DisplayTotals(IRegressionFunction timeRegressionFunction,
                                   ChromatogramSet chromatograms,
                                   float mzMatchTolerance,
                                   int countLabelTypes,
                                   ref double bestStartTime,
                                   ref double bestEndTime)
        {
            // Construct and add graph items for all relevant transition groups.
            float fontSize = FontSize;
            int lineWidth = LineWidth;
            int iCharge = -1, charge = -1;
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
                foreach (TransitionDocNode nodeTran in nodeGroup.Children)
                {
                    var info = chromGroupInfo.GetTransitionInfo((float)nodeTran.Mz, mzMatchTolerance);
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
                    AddBestPeakTimes(transitionChromInfo, ref bestStartTime, ref bestEndTime);
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
                    Color color = COLORS_GROUPS[iColor % COLORS_GROUPS.Length];

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
                                                       0,
                                                       color,
                                                       fontSize,
                                                       lineWidth);
                    var graphPaneKey = new GraphHelper.PaneKey(nodeGroup);
                    _graphHelper.AddChromatogram(graphPaneKey, graphItem);
                }
            }
        }

        private void SetRetentionTimeIndicators(ChromGraphItem chromGraphPrimary,
                                                SrmSettings settings,
                                                ChromatogramSet chromatograms,
                                                TransitionGroupDocNode[] nodeGroups,
                                                ExplicitMods mods)
        {
            SetRetentionTimePredictedIndicator(chromGraphPrimary, settings, chromatograms, nodeGroups, mods);
            SetRetentionTimeIdIndicators(chromGraphPrimary, settings, nodeGroups, mods);
        }

        private static void SetRetentionTimePredictedIndicator(ChromGraphItem chromGraphPrimary, SrmSettings settings,
                                                               ChromatogramSet chromatograms,
                                                               TransitionGroupDocNode[] nodeGroups, ExplicitMods mods)
        {
            // Set predicted retention time on the first graph item to make
            // line, label and shading show.
            var regression = settings.PeptideSettings.Prediction.RetentionTime;
            if (regression != null && Settings.Default.ShowRetentionTimePred)
            {
                string sequence = nodeGroups[0].TransitionGroup.Peptide.Sequence;
                string modSeq = settings.GetModifiedSequence(sequence, IsotopeLabelType.light, mods);
                var fileId = chromatograms.FindFile(chromGraphPrimary.Chromatogram);
                double? predictedRT = regression.GetRetentionTime(modSeq, fileId);
                double window = regression.TimeWindow;

                chromGraphPrimary.RetentionPrediction = predictedRT;
                chromGraphPrimary.RetentionWindow = window;
            }
        }


        private void SetRetentionTimeIdIndicators(ChromGraphItem chromGraphPrimary, SrmSettings settings,
                                                  IEnumerable<TransitionGroupDocNode> nodeGroups, ExplicitMods mods)
        {
            // Set any MS/MS IDs on the first graph item also
            if (settings.TransitionSettings.FullScan.IsEnabled &&
                settings.PeptideSettings.Libraries.IsLoaded)
            {
                var transitionGroups = nodeGroups.Select(nodeGroup => nodeGroup.TransitionGroup).ToArray();
                if (Settings.Default.ShowPeptideIdTimes)
                {
                    var listTimes = new List<double>();
                    foreach (var group in transitionGroups)
                    {
                        IsotopeLabelType labelType;
                        double[] retentionTimes;
                        if (settings.TryGetRetentionTimes(group.Peptide.Sequence, group.PrecursorCharge,
                                                          mods, FilePath, out labelType, out retentionTimes))
                        {
                            listTimes.AddRange(retentionTimes);
                            var selectedSpectrum = _stateProvider.SelectedSpectrum;
                            if (selectedSpectrum != null && Equals(FilePath, selectedSpectrum.FilePath))
                            {
                                chromGraphPrimary.SelectedRetentionMsMs = selectedSpectrum.RetentionTime;
                            }
                        }
                    }
                    if (listTimes.Count > 0)
                        chromGraphPrimary.RetentionMsMs = listTimes.ToArray();
                }
                if (Settings.Default.ShowAlignedPeptideIdTimes)
                {
                    var listTimes = new List<double>();
                    foreach (var group in transitionGroups)
                    {
                        listTimes.AddRange(settings.GetAlignedRetentionTimes(FilePath, group.Peptide.Sequence, mods));
                    }
                    if (listTimes.Count > 0)
                    {
                        var sortedTimes = listTimes.Distinct().ToArray();
                        Array.Sort(sortedTimes);
                        chromGraphPrimary.AlignedRetentionMsMs = sortedTimes;
                    }
                }
                if (Settings.Default.ShowUnalignedPeptideIdTimes)
                {
                    var listTimes = new List<double>();
                    foreach (var group in transitionGroups)
                    {
                        listTimes.AddRange(settings.GetUnalignedRetentionTimes(group.Peptide.Sequence, mods));
                    }
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
            if (!nodeTran.HasResults)
                return null;
            var tranChromInfoList = nodeTran.Results[indexChrom];
            if (tranChromInfoList == null)
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
            if (!nodeGroup.HasResults)
                return null;
            var tranGroupChromInfoList = nodeGroup.Results[indexChrom];
            if (tranGroupChromInfoList == null)
                return null;
            foreach (var tranGroupChromInfo in tranGroupChromInfoList)
            {
                if (ReferenceEquals(tranGroupChromInfo.FileId, fileId))
                    return tranGroupChromInfo;
            }
            return null;
        }

        private static void AddBestPeakTimes(TransitionChromInfo chromInfo, ref double bestStartTime,
                                             ref double bestEndTime)
        {
            // Make sure all parts of the best peak are included in the
            // best peak window
            double end = chromInfo.EndRetentionTime;
            if (end <= 0)
                return;
            double start = chromInfo.StartRetentionTime;

            bestStartTime = Math.Min(bestStartTime, start);
            bestEndTime = Math.Max(bestEndTime, end);
        }

        private void UpdateToolbar(ICollection<ChromatogramGroupInfo[]> arrayChromInfo)
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
                foreach (var arrayInfo in arrayChromInfo)
                {
                    string name = string.Empty;
                    foreach (var info in arrayInfo)
                    {
                        if (info != null)
                        {
                            name = SampleHelp.GetPathSampleNamePart(info.FilePath);
                            if (string.IsNullOrEmpty(name))
                                name = Path.GetFileName(info.FilePath);
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
                    if (selected == null || comboFiles.Items.IndexOf(selected) == -1)
                        comboFiles.SelectedIndex = 0;
                    else
                        comboFiles.SelectedItem = selected;
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
            if (UpdateGroups(nodeGroups, groupPaths, out changedGroups, out changedGroupIds) && _extractor == extractor)
                return true;

            _extractor = extractor;

            bool success = false;
            try
            {
                // Get chromatogram sets for all transition groups, recording unique
                // file paths in the process.
                var listArrayChromInfo = new List<ChromatogramGroupInfo[]>();
                var listFiles = new List<string>();
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
                        string filePath = chromInfo.FilePath;
                        if (!listFiles.Contains(filePath))
                            listFiles.Add(filePath);
                    }
                }

                _arrayChromInfo = new ChromatogramGroupInfo[listFiles.Count][];
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

                success = true;
            }
            finally
            {
                // Make sure the info array is set to null on failure.
                if (!success)
                    _arrayChromInfo = null;
            }

            return true;
        }

        private bool EnsureChromInfo(MeasuredResults results,
                                     ChromatogramSet chromatograms,
                                     PeptideDocNode nodePep,
                                     TransitionGroupDocNode[] nodeGroups,
                                     IdentityPath[] groupPaths,
                                     float mzMatchTolerance,
                                     out bool changedGroups,
                                     out bool changedGroupIds)
        {
            if (UpdateGroups(nodeGroups, groupPaths, out changedGroups, out changedGroupIds) && !_extractor.HasValue)
                return true;

            _extractor = null;

            bool success = false;
            try
            {
                // Get chromatogram sets for all transition groups, recording unique
                // file paths in the process.
                var listArrayChromInfo = new List<ChromatogramGroupInfo[]>();
                var listFiles = new List<string>();
                foreach (var nodeGroup in nodeGroups)
                {
                    ChromatogramGroupInfo[] arrayChromInfo;
                    if (!results.TryLoadChromatogram(chromatograms, nodePep, nodeGroup, mzMatchTolerance, true,
                                                     out arrayChromInfo))
                    {
                        listArrayChromInfo.Add(null);
                        continue;
                    }

                    listArrayChromInfo.Add(arrayChromInfo);
                    foreach (var chromInfo in arrayChromInfo)
                    {
                        string filePath = chromInfo.FilePath;
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
                success = true;
            }
            finally
            {
                // Make sure the info array is set to null on failure.
                if (!success)
                    _arrayChromInfo = null;
            }

            return true;
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

        private ScaledRetentionTime FindAnnotatedPeakRetentionTime(TextObj label,
                                                                   out TransitionGroupDocNode nodeGroup,
                                                                   out TransitionDocNode nodeTran)
        {
            foreach (var graphItem in GraphItems)
            {
                var peakRT = graphItem.FindPeakRetentionTime(label);
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

            foreach (var curveCurr in graphPane.CurveList)
            {
                var graphItemCurr = curveCurr.Tag as ChromGraphItem;
                if (graphItemCurr == null)
                    continue;
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
                return graphItem.GetNearestDisplayTime(displayTime);
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

                foreach (var curve in graphPane.CurveList)
                {
                    var graphItemNext = curve.Tag as ChromGraphItem;
                    if (null == graphItemNext)
                    {
                        continue;
                    }
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
            get { return GraphPanes.SelectMany(pane=>pane.CurveList.Select(curve=>(ChromGraphItem) curve.Tag)); }
        }

        public double[] RetentionMsMs
        {
            get
            {
                return (from graphItem in GraphItems
                        where graphItem.RetentionMsMs != null
                        select graphItem.RetentionMsMs).FirstOrDefault();
            }
        }

        public double[] AlignedRetentionMsMs
        {
            get
            {
                return (from graphItem in GraphItems
                        where graphItem.AlignedRetentionMsMs != null
                        select graphItem.AlignedRetentionMsMs).FirstOrDefault();
            }
        }

        private PeakBoundsDragInfo[] _peakBoundDragInfos;

        private bool graphControl_MouseMoveEvent(ZedGraphControl sender, MouseEventArgs e)
        {
            PointF pt = new PointF(e.X, e.Y);

            if (_peakBoundDragInfos != null && _peakBoundDragInfos.Length > 0)
            {
                graphControl.Cursor = Cursors.VSplit;
                if (DoDrag(_peakBoundDragInfos.First().GraphPane, pt))
                    Refresh();
                return true;
            }

            if (e.Button != MouseButtons.None)
                return false;

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
                        TransitionGroupDocNode nodeGroup;
                        TransitionDocNode nodeTran;
                        if ((!_extractor.HasValue && !FindAnnotatedPeakRetentionTime(label, out nodeGroup, out nodeTran).IsZero) ||
                            !FindAnnotatedSpectrumRetentionTime(label).IsZero)
                        {
                            graphControl.Cursor = Cursors.Hand;
                            return true;
                        }
                    }
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

                    if (nearest is CurveItem)
                    {
                        graphControl.Cursor = Cursors.VSplit;
                        return true;
                    }
                    if (nearest is XAxis && IsGroupActive)
                    {
                        graphControl.Cursor = Cursors.VSplit;
                        return true;
                    }

                }
            }
            return false;
        }

        private bool graphControl_MouseDownEvent(ZedGraphControl sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                PointF pt = new PointF(e.X, e.Y);
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

                        CurveItem[] changeCurves = null;
                        // If clicked on the XAxis for a graph of a single precursor, use its first curve
                        var item = nearest as CurveItem;
                        if (item != null)
                            changeCurves = new[] {item};
                        else if (nearest is XAxis && IsGroupActive)
                        {
                            changeCurves = IsMultiGroup
                                               ? nearestGraphPane.CurveList.ToArray()
                                               : new[] {nearestGraphPane.CurveList.First()};
                        }
                        if (changeCurves != null)
                        {
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
                if (_peakBoundDragInfos != null && _peakBoundDragInfos.Length > 0)
                {
                    PointF pt = new PointF(e.X, e.Y);
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
            _stateProvider.BuildChromatogramMenu(sender, menuStrip, GetChromFileInfoId());
        }

        protected override void OnClosed(EventArgs e)
        {
            _documentContainer.UnlistenUI(OnDocumentUIChanged);
        }

        public static readonly Color[] COLORS_GROUPS =
            {
                ChromGraphItem.ColorSelected,
                Color.Blue,
                Color.Maroon,
                Color.Purple,
                Color.Orange,
                Color.Green,
                Color.Yellow,
                Color.LightBlue,
            };

        public static readonly Color[] COLORS_LIBRARY =
            {
                Color.Blue,
                Color.BlueViolet,
                Color.Brown,
                Color.Chocolate,
                Color.DarkCyan,
                Color.Green,
                Color.Orange,
//                Color.Navy,
                Color.FromArgb(0x75, 0x70, 0xB3),
                Color.Purple,
                Color.LimeGreen,
                Color.Gold,
                Color.Magenta,
                Color.Maroon,
                Color.OliveDrab,
                Color.RoyalBlue,
            };

//        public static readonly Color[] COLORS_MANI =
//            {
//                Color.FromArgb(0x1B,0x9E,0x77), 
//                Color.FromArgb(0x37,0x7E,0xB8),
//                Color.FromArgb(0x4D,0xAF,0x4A),
//                Color.FromArgb(0x75,0x70,0xB3),
//                Color.FromArgb(0x98,0x4E,0xA3),
//                Color.FromArgb(0x99,0x99,0x99),
//                Color.FromArgb(0xA6,0x56,0x28),
//                Color.FromArgb(0xD9,0x5F,0x02),
//                Color.FromArgb(0xE4,0x1A,0x1C),
//                Color.FromArgb(0xE6,0xAB,0x02),
//                Color.FromArgb(0xE7,0x29,0x8A),
//                Color.FromArgb(0xF7,0x81,0xBF),
//                Color.FromArgb(0xFF,0x7F,0x00),
//            };

//        private static readonly Color[] COLORS_HEURISTIC =
//            {
//                Color.SkyBlue,
//                Color.Plum,
//                Color.Peru,
//                Color.Moccasin,
//                Color.LightSeaGreen,
//                Color.LightGreen,
//                Color.LightSalmon,
//                Color.LightCoral,
//                Color.MediumTurquoise,
//                Color.Plum,
//                Color.PaleGreen,
//                Color.Pink,
//            };

        public static int GetColorIndex(TransitionGroupDocNode nodeGroup, int countLabelTypes, ref int charge,
                                        ref int iCharge)
        {
            // Make sure colors stay somewhat consistent among charge states.
            // The same label type should always have the same color, with the
            // first charge state in the peptide matching the peptide label type
            // modification font colors.
            if (charge != nodeGroup.TransitionGroup.PrecursorCharge)
            {
                charge = nodeGroup.TransitionGroup.PrecursorCharge;
                iCharge++;
            }
            return iCharge * countLabelTypes + nodeGroup.TransitionGroup.LabelType.SortOrder;
        }

        public class ListChromGraphs : List<KeyValuePair<GraphHelper.PaneKey, ChromGraphItem>>
        {
            public void Add(GraphHelper.PaneKey graphPaneKey, ChromGraphItem chromGraphItem)
            {
                Add(new KeyValuePair<GraphHelper.PaneKey, ChromGraphItem>(graphPaneKey, chromGraphItem));
            }

            public IEnumerable<ChromGraphItem> PrimaryGraphItems
            {
                get { return PrimaryGraphPaneKeyItems.Select(keyValuePair=>keyValuePair.Value); }
            }
            public IEnumerable<KeyValuePair<GraphHelper.PaneKey, ChromGraphItem>> PrimaryGraphPaneKeyItems
            {
                get { return this.ToLookup(kvp => kvp.Key).Select(grouping => grouping.Last()); }
            }
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

            var rtNew = GraphItem.GetNearestDisplayTime(time);
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
        protected PeakEventArgs(IdentityPath groupPath, string nameSet, string filePath)
        {
            GroupPath = groupPath;
            NameSet = nameSet;
            FilePath = filePath;
        }

        public IdentityPath GroupPath { get; private set; }
        public string NameSet { get; private set; }
        public string FilePath { get; private set; }
    }

    public sealed class PickedPeakEventArgs : PeakEventArgs
    {
        public PickedPeakEventArgs(IdentityPath groupPath, Identity transitionId,
                                   string nameSet, string filePath, ScaledRetentionTime retentionTime)
            : base(groupPath, nameSet, filePath)
        {
            TransitionId = transitionId;
            RetentionTime = retentionTime;
        }

        public Identity TransitionId { get; private set; }
        public ScaledRetentionTime RetentionTime { get; private set; }
    }

    public sealed class ChangedPeakBoundsEventArgs : PeakEventArgs
    {
        public ChangedPeakBoundsEventArgs(IdentityPath groupPath,
                                          Transition transition,
                                          string nameSet,
                                          string filePath,
                                          ScaledRetentionTime startTime,
                                          ScaledRetentionTime endTime,
                                          PeakIdentification identified,
                                          PeakBoundsChangeType changeType)
            : base(groupPath, nameSet, filePath)
        {
            Transition = transition;
            StartTime = startTime;
            EndTime = endTime;
            Identified = identified;
            ChangeType = changeType;
        }

        public Transition Transition { get; private set; }
        public ScaledRetentionTime StartTime { get; private set; }
        public ScaledRetentionTime EndTime { get; private set; }
        public PeakIdentification Identified { get; private set; }
        public bool IsIdentified { get { return Identified != PeakIdentification.FALSE; } }
        public PeakBoundsChangeType ChangeType { get; private set; }
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
            return string.Format("{0} ({1})", MeasuredTime, DisplayTime);
        }
    }
}