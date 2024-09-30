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
using System.Windows.Forms;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using ZedGraph;

namespace pwiz.Skyline.Controls.Graphs
{
    public partial class GraphSummary : DockableFormEx, IUpdatable, IMultipleViewProvider
    {
        private const string FONT_FACE = "Arial";

        public static Color ColorSelected { get { return Color.Red; } }

        public static FontSpec CreateFontSpec(Color color)
        {
            return new FontSpec(FONT_FACE, Settings.Default.AreaFontSize, color, false, false, false, Color.Empty, null, FillType.None)
            {
                Border = { IsVisible = false },
                StringAlignment = StringAlignment.Near
            };
        }

        public interface IController
        {
            GraphSummary GraphSummary { get; set; }

            UniqueList<GraphTypeSummary> GraphTypes { get; set; }

            void OnDocumentChanged(SrmDocument oldDocument, SrmDocument newDocument);
            void OnActiveLibraryChanged();
            void OnResultsIndexChanged();

            void OnUpdateGraph();

            bool HandleKeyDownEvent(object sender, KeyEventArgs e);

            IFormView FormView { get; }

            string Text { get; }
        }

        public interface IControllerSplit : IController
        {
            bool IsReplicatePane(SummaryGraphPane pane);
            bool IsPeptidePane(SummaryGraphPane pane);
            SummaryGraphPane CreateReplicatePane(PaneKey key);
            SummaryGraphPane CreatePeptidePane(PaneKey key);
        }

        public class RTGraphView : IFormView {}
        public class AreaGraphView : IFormView {}
        public class DetectionsGraphView : IFormView { }

        public interface IStateProvider
        {
            SrmDocument SelectionDocument { get; }

            TreeNodeMS SelectedNode { get; }
            IList<TreeNodeMS> SelectedNodes { get; }
            IdentityPath SelectedPath { get; set; }
            void SelectPath(IdentityPath path);
            PeptideGraphInfo GetPeptideGraphInfo(DocNode docNode);
            int SelectedResultsIndex { get; set; }

            GraphValues.IRetentionTimeTransformOp GetRetentionTimeTransformOperation();

            void ActivateSpectrum();

            void BuildGraphMenu(ZedGraphControl zedGraphControl, ContextMenuStrip menuStrip, Point mousPt, IController controller);

            NormalizeOption AreaNormalizeOption { get; set; }
        }

        private class DefaultStateProvider : IStateProvider
        {
            public SrmDocument SelectionDocument { get { return null;}}
            public TreeNodeMS SelectedNode { get { return null; } }
            public IList<TreeNodeMS> SelectedNodes { get { return null; } }
            public IdentityPath SelectedPath { get { return IdentityPath.ROOT; } set { } }
            public void BuildGraphMenu(ZedGraphControl zedGraphControl, ContextMenuStrip menuStrip, Point mousePt, IController controller) { }
            public int SelectedResultsIndex { get; set; }
            public void ActivateSpectrum() {}
            public GraphValues.IRetentionTimeTransformOp GetRetentionTimeTransformOperation() {return null;}
            public void SelectPath(IdentityPath path){}
            public PeptideGraphInfo GetPeptideGraphInfo(DocNode docNode)
            {
                return null;
            }

            public NormalizeOption AreaNormalizeOption
            {
                get => NormalizeOption.NONE;
                set { }
            }
        }

        private readonly IDocumentUIContainer _documentContainer;
        private readonly IStateProvider _stateProvider;
        private readonly IController _controller;

        private bool _activeLibrary;
        private int _targetResultsIndex;
        private int _originalResultsIndex;

        private GraphSummaryToolbar _toolbar;
        public GraphSummaryToolbar Toolbar
        {
            get { return _toolbar; }
            set
            {
                if (value != null)
                {
                    _toolbar = value;
                    splitContainer.Panel1.Controls.Clear();
                    splitContainer.Panel1.Controls.Add(value);
                }
            }
        }

        public string LabelLayoutString { get; set; }

        public GraphTypeSummary Type { get; set; }

        public GraphSummary(GraphTypeSummary type, IDocumentUIContainer documentUIContainer, IController controller, int targetResultsIndex, int originalIndex = -1)
        {
            _targetResultsIndex = targetResultsIndex;
            _originalResultsIndex = originalIndex;
            InitializeComponent();

            Icon = Resources.SkylineData;

            graphControl.MasterPane.Border.IsVisible = false;

            _controller = controller;
            _controller.GraphSummary = this;

            _documentContainer = documentUIContainer;
            _documentContainer.ListenUI(OnDocumentUIChanged);
            _stateProvider = documentUIContainer as IStateProvider ??
                             new DefaultStateProvider();

            Type = type;
            Text = Controller.Text + @" - " + Type.CustomToString();
            Helpers.PeptideToMoleculeTextMapper.TranslateForm(this, _documentContainer.Document.DocumentType); // Use terminology like "Molecule Comparison" instead of "Peptide Comparison" as appropriate

            UpdateUI();
        }

        public bool ActiveLibrary
        {
            get { return _activeLibrary;  }
            set
            {
                if(_activeLibrary != value)
                {
                    _activeLibrary = value;

                    _controller.OnActiveLibraryChanged();
                }
            }
        }

        public int ResultsIndex
        {
            get { return _targetResultsIndex; } // Synonym to avoid making other code use Target
        }

        public int TargetResultsIndex
        {
            get { return _targetResultsIndex; }
        }

        public int OriginalResultsIndex
        {
            get { return _originalResultsIndex; }

        }

        public void SetResultIndexes(int target, int original = -1, bool updateIfChanged = true)
        {
            bool update = target != _targetResultsIndex || original != _originalResultsIndex;
            _targetResultsIndex = target;
            _originalResultsIndex = original;
            if (update && updateIfChanged)
                _controller.OnResultsIndexChanged();
        }

        public NormalizeOption NormalizeOption
        {
            get { return StateProvider.AreaNormalizeOption; }
            set
            {
                StateProvider.AreaNormalizeOption = value;
            }
        }

        public IController Controller { get { return _controller; } }

        public IFormView ShowingFormView { get { return _controller.FormView; } }

        public IStateProvider StateProvider { get { return _stateProvider; } }

        public ZedGraphControl GraphControl { get { return graphControl; } }

        public IDocumentUIContainer DocumentUIContainer { get { return _documentContainer; } }

        public void OnDocumentUIChanged(object sender, DocumentChangedEventArgs e)
        {
            _controller.OnDocumentChanged(e.DocumentPrevious, DocumentUIContainer.DocumentUI);
            if(HasToolbar)
                Toolbar.OnDocumentChanged(e.DocumentPrevious, DocumentUIContainer.DocumentUI);

            UpdateUI();
        }

        private void GraphSummary_VisibleChanged(object sender, EventArgs e)
        {
            UpdateUI();
        }

        private void GraphSummary_KeyDown(object sender, KeyEventArgs e)
        {
            if (GraphPanes.First().HandleKeyDownEvent(sender, e))
                return;

            if (_controller.HandleKeyDownEvent(sender, e))
                return;

            switch (e.KeyCode)
            {
                case Keys.Escape:
                    _documentContainer.FocusDocument();
                    break;
            }
        }

        public int CurveCount { get { return CountCurves(c => true); } }

        public int CountCurves(Func<CurveItem, bool> isCounted)
        {
            return GraphPanes.Sum(pane => pane.CurveList.Count(isCounted));
        }

        internal IEnumerable<SummaryGraphPane> GraphPanes
        {
            get { return graphControl.MasterPane.PaneList.OfType<SummaryGraphPane>(); }
            set
            {
                graphControl.MasterPane.PaneList.OfType<SummaryGraphPane>().ForEach(panel => panel.OnClose(EventArgs.Empty));
                graphControl.MasterPane.PaneList.Clear();
                graphControl.MasterPane.PaneList.AddRange(value);
            }
        }

        public bool TryGetGraphPane<TPane>(out TPane pane) where TPane : class
        {
            pane = GraphPanes.FirstOrDefault() as TPane;
            return (pane != null);
        }

        protected override string GetPersistentString()
        {
            var res = base.GetPersistentString() + '|' + _controller.GetType().Name + '|' + Type;
            var panelLayouts = GraphPanes.OfType<ILayoutPersistable>().FirstOrDefault()?.GetPersistentString();
            if (panelLayouts != null)
            {
                res = res + '|' + Uri.EscapeDataString(panelLayouts);
            }
            return res;
        }

        public IEnumerable<string> Categories
        {
            get { return GraphPanes.First().XAxis.Scale.TextLabels; }
        }

        public void UpdateUI(bool selectionChanged = true)
        {
            UpdateGraph(selectionChanged);
            UpdateToolbar();
        }

        public void UpdateUIWithoutToolbar(bool selectionChanged = true)
        {
            UpdateGraph(selectionChanged);
        }

        private bool SplitterDistanceValid(double distance)
        {
            return distance > splitContainer.Panel1MinSize &&
                   distance < splitContainer.Height - splitContainer.Panel2MinSize;
        }

        private void UpdateToolbar()
        {
            if (!Visible || IsDisposed || Toolbar == null || !SplitterDistanceValid(Toolbar.Height))
                return;

            if (!ReferenceEquals(DocumentUIContainer.Document, StateProvider.SelectionDocument))
                return;

            if (HasToolbar)
            {
                Toolbar.UpdateUI();
                splitContainer.SplitterDistance = Toolbar.Height;
                splitContainer.Panel1Collapsed = !_toolbar.Visible;
            }
            else
            {
                splitContainer.Panel1Collapsed = true;
            }
        }

        public bool HasToolbar { get { return Toolbar != null && GraphPanes.Count() == 1 && GraphPanes.First().HasToolbar; } }

        private void UpdateGraph(bool selectionChanged)
        {
            // Only worry about updates, if the graph is visible
            // And make sure it is not disposed, since rendering happens on a timer
            if (!Visible || IsDisposed)
                return;

            // Avoid updating when document container and state provider are out of sync
            if (!ReferenceEquals(DocumentUIContainer.Document, StateProvider.SelectionDocument))
                return;

            var graphPanesCurrent = GraphPanes.ToArray();
            _controller.OnUpdateGraph();
            var graphPanes = GraphPanes.ToArray();

            if (!graphPanesCurrent.SequenceEqual(graphPanes))
            {
                foreach (var pane in graphPanesCurrent)
                {
                    // Release any necessary resources from the old pane
                    var disposable = pane as IDisposable;
                    if (disposable != null)
                        disposable.Dispose();   
                }

                // Layout the new pane
                using (Graphics g = CreateGraphics())
                {
                    graphControl.MasterPane.SetLayout(g, PaneLayout.SingleColumn);
                }                
            }

            foreach (var pane in graphPanes)
            {
                pane.UpdateGraph(selectionChanged);
                GraphHelper.FormatGraphPane(pane);
                GraphHelper.FormatFontSize(pane, Settings.Default.AreaFontSize);
            }
            graphControl.Invalidate();
        }

        private void graphControl_ContextMenuBuilder(ZedGraphControl sender, ContextMenuStrip menuStrip, Point mousePt, ZedGraphControl.ContextMenuObjectState objState)
        {
            _stateProvider.BuildGraphMenu(sender, menuStrip, mousePt, _controller);
        }

        private bool graphControl_MouseMoveEvent(ZedGraphControl sender, MouseEventArgs e)
        {
            var graphPane = GraphPaneFromPoint(e.Location);
            if (graphPane == null)
                return false;
            if (sender != null && e.Button == sender.PanButtons && ModifierKeys == sender.PanModifierKeys)
                graphPane.EnsureYMin();
            return graphPane.HandleMouseMoveEvent(sender, e);
        }

        private void graphControl_MouseOutEvent(object sender, EventArgs e)
        {
            foreach(var pane in graphControl.MasterPane.PaneList)
                (pane as SummaryGraphPane)?.HandleMouseOutEvent(sender, e);
        }

        private bool graphControl_MouseDownEvent(ZedGraphControl sender, MouseEventArgs e)
        {
            var graphPane = GraphPaneFromPoint(e.Location);
            return null != graphPane && graphPane.HandleMouseDownEvent(sender, e);
        }

        private void graphControl_MouseClick(object sender, MouseEventArgs e)
        {
            var graphPane = GraphPaneFromPoint(e.Location);
            if(graphPane != null)
                graphPane.HandleMouseClick(sender, e);
        }

        private void graphControl_ZoomEvent(ZedGraphControl sender, ZoomState oldState, ZoomState newState, PointF mousePosition)
        {
            foreach (var pane in GraphPanes)
            {
                pane.EnsureYMin();
            }
        }

        public SummaryGraphPane GraphPaneFromPoint(PointF point)
        {
            if (graphControl.MasterPane.PaneList.Count == 1)
            {
                return graphControl.MasterPane.PaneList[0] as SummaryGraphPane;
            }
            else
            {
                return graphControl.MasterPane.FindPane(point) as SummaryGraphPane;
            }
        }


        protected override void OnClosed(EventArgs e)
        {
            _documentContainer.UnlistenUI(OnDocumentUIChanged);
            foreach (var summaryGraphPane in GraphPanes)
                summaryGraphPane.OnClose(e);
        }

        private void GraphSummary_Resize(object sender, EventArgs e)
        {
            // Apparently on Windows 7, a resize event may occur during InitializeComponent
            foreach (var pane in GraphPanes)
            {
                pane.HandleResizeEvent();
            }
        }

        public void DoUpdateGraph(IControllerSplit graphController, GraphTypeSummary graphType)
        {
            var paneKeys = CalcPaneKeys(graphType);

            bool panesValid = paneKeys.SequenceEqual(GraphPanes.Select(pane => pane.PaneKey));
            if (panesValid)
            {
                switch (graphType)
                {
                    case GraphTypeSummary.replicate:
                        panesValid = GraphPanes.All(graphController.IsReplicatePane);
                        break;
                    case GraphTypeSummary.peptide:
                        panesValid = GraphPanes.All(graphController.IsPeptidePane);
                        break;
                }
            }
            if (panesValid)
            {
                return;
            }

            switch (graphType)
            {
                case GraphTypeSummary.replicate:
                    GraphPanes = paneKeys.Select(graphController.CreateReplicatePane);
                    break;
                case GraphTypeSummary.peptide:
                    GraphPanes = paneKeys.Select(graphController.CreatePeptidePane);
                    break;
            }
        }

        public PaneKey[] CalcPaneKeys(GraphTypeSummary graphType)
        {
            PaneKey[] paneKeys = null;
            if (Settings.Default.SplitChromatogramGraph)
            {
                if (graphType == GraphTypeSummary.replicate)
                {
                    var selectedTreeNode = StateProvider.SelectedNode as SrmTreeNode;
                    if (null != selectedTreeNode)
                    {
                        TransitionGroupDocNode[] transitionGroups;
                        bool transitionSelected = false;
                        // ReSharper disable once CanBeReplacedWithTryCastAndCheckForNull
                        if (selectedTreeNode.Model is PeptideDocNode)
                        {
                            transitionGroups = ((PeptideDocNode)selectedTreeNode.Model).TransitionGroups.ToArray();
                        }
                        // ReSharper disable once CanBeReplacedWithTryCastAndCheckForNull
                        else if (selectedTreeNode.Model is TransitionGroupDocNode)
                        {
                            transitionGroups = new[] { (TransitionGroupDocNode)selectedTreeNode.Model };
                        }
                        else if (selectedTreeNode.Model is TransitionDocNode)
                        {
                            transitionGroups = new[] { (TransitionGroupDocNode)((SrmTreeNode)selectedTreeNode.Parent).Model };
                            transitionSelected = true;
                        }
                        else
                        {
                            transitionGroups = new TransitionGroupDocNode[0];
                        }
                        if (transitionGroups.Length == 1)
                        {
                            if (GraphChromatogram.DisplayType == DisplayTypeChrom.all
                                || (GraphChromatogram.DisplayType == DisplayTypeChrom.single && !transitionSelected))
                            {
                                var transitionGroup = transitionGroups[0];
                                bool hasPrecursors = transitionGroup.Transitions.Any(transition => transition.IsMs1);
                                bool hasProducts = transitionGroup.Transitions.Any(transition => !transition.IsMs1);
                                if (hasPrecursors && hasProducts && !IsOptimization(transitionGroup))
                                {
                                    paneKeys = new[] { PaneKey.PRECURSORS, PaneKey.PRODUCTS };
                                }
                            }
                        }
                        else if (transitionGroups.Length > 1)
                        {
                            paneKeys = transitionGroups.Select(group => new PaneKey(@group))
                                .Distinct().ToArray();
                        }
                    }
                }
                else
                {
                    IEnumerable<PeptideGroupDocNode> moleculeGroups = StateProvider.SelectionDocument.MoleculeGroups;
                    if (AreaGraphController.AreaScope == AreaScope.protein)
                    {
                        var peptideGroupDocNode = (StateProvider.SelectedNode as SrmTreeNode)
                            ?.GetNodeOfType<PeptideGroupTreeNode>()?.DocNode;
                        if (peptideGroupDocNode != null)
                        {  
                            moleculeGroups = new[] { peptideGroupDocNode };
                        }
                    }

                    paneKeys = moleculeGroups
                        .SelectMany(group => group.Peptides.SelectMany(peptide => peptide.TransitionGroups
                            .Select(tg => tg.LabelType)))
                        .Distinct().Select(labelType => new PaneKey(labelType)).ToArray();
                }
            }
            paneKeys = paneKeys ?? new[] { PaneKey.DEFAULT };
            Array.Sort(paneKeys);
            return paneKeys;
        }

        private static bool IsOptimization(TransitionGroupDocNode nodeTranGroup)
        {
            if (nodeTranGroup.Transitions.Any(nodeTran => nodeTran.IsMs1 && nodeTran.ChromInfos.Any()))
                return false;

            var steps = nodeTranGroup.ChromInfos.Select(info => info.OptimizationStep).OrderBy(step => step).ToArray();
            if (steps.Length <= 1)
                return false;

            for (var i = 0; i < steps.Length - 1; i++)
            {
                if (steps[i] != steps[i + 1] - 1)
                    return false;
            }

            return true;
        }
    }

    [Flags]
    public enum GraphTypeSummary
    {
        invalid = 0,
        replicate = 1,
        peptide = 1 << 1,
        score_to_run_regression = 1 << 2,
        schedule = 1 << 3,
        run_to_run_regression = 1 << 4,
        histogram = 1 << 5,
        histogram2d = 1 << 6,
        detections = 1 << 7,
        detections_histogram = 1 << 8,
        abundance = 1 << 9
    }

    public static class Extensions
    {
        public static string CustomToString(this GraphTypeSummary type)
        {
            switch (type)
            {
                case GraphTypeSummary.invalid:
                    return string.Empty;
                case GraphTypeSummary.replicate:
                    return GraphsResources.Extensions_CustomToString_Replicate_Comparison;
                case GraphTypeSummary.peptide:
                    return GraphsResources.Extensions_CustomToString_Peptide_Comparison;
                case GraphTypeSummary.abundance:
                    return GraphsResources.Extensions_CustomToString_Relative_Abundance;
                case GraphTypeSummary.score_to_run_regression:
                    return GraphsResources.Extensions_CustomToString_Score_To_Run_Regression;
                case GraphTypeSummary.schedule:
                    return GraphsResources.Extensions_CustomToString_Scheduling;
                case GraphTypeSummary.run_to_run_regression:
                    return GraphsResources.Extensions_CustomToString_Run_To_Run_Regression;
                case GraphTypeSummary.histogram:
                    return GraphsResources.Extensions_CustomToString_Histogram;
                case GraphTypeSummary.histogram2d:
                    return GraphsResources.Extensions_CustomToString__2D_Histogram;
                case GraphTypeSummary.detections:
                    return GraphsResources.Extensions_CustomToString_Detections_Replicates;
                case GraphTypeSummary.detections_histogram:
                    return GraphsResources.Extensions_CustomToString_Detections_Histogram;
                default:
                    return string.Empty;
            }
        }
    }
}
