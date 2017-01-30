﻿/*
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
using pwiz.Common.Controls;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using ZedGraph;

namespace pwiz.Skyline.Controls.Graphs
{
    public enum GraphTypeSummary { replicate, peptide }

    public partial class GraphSummary : DockableFormEx, IUpdatable, IMultipleViewProvider
    {
        private const string FONT_FACE = "Arial"; // Not L10N

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

            void OnActiveLibraryChanged();
            void OnResultsIndexChanged();
            void OnRatioIndexChanged();

            void OnUpdateGraph();

            bool HandleKeyDownEvent(object sender, KeyEventArgs e);

            IFormView FormView { get; }

            bool IsRunToRun();
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
        }

        private readonly IDocumentUIContainer _documentContainer;
        private readonly IStateProvider _stateProvider;
        private readonly IController _controller;

        private bool _activeLibrary;
        private int _targetResultsIndex;
        private int _originalResultsIndex;

        private int _ratioIndex;

        public GraphSummary(IDocumentUIContainer documentUIContainer, IController controller, int targetResultsIndex, int originalIndex = -1)
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
            ResizeToolbar();
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

        public void SetResultIndexes(int target, int original = -1)
        {
            bool update = target != _targetResultsIndex || original != _originalResultsIndex;
            _targetResultsIndex = target;
            _originalResultsIndex = original;
            if(update)
                _controller.OnResultsIndexChanged();
        }

        public bool IsRunToRun { get { return Controller.IsRunToRun(); } }

        /// <summary>
        /// Not all summary graphs care about this value, but since the
        /// peak area summary graph uses this class directly, this is the
        /// only way to get it the ratio index value.
        /// </summary>
        public int RatioIndex
        {
            get { return _ratioIndex; }
            set
            {
                if (_ratioIndex != value)
                {
                    _ratioIndex = value;

                    _controller.OnRatioIndexChanged();
                }
            }
        }

        public IController Controller { get { return _controller; } }

        public IFormView ShowingFormView { get { return _controller.FormView; } }

        public IStateProvider StateProvider { get { return _stateProvider; } }

        public ZedGraphControl GraphControl { get { return graphControl; } }

        public IDocumentUIContainer DocumentUIContainer { get { return _documentContainer; } }

        public void OnDocumentUIChanged(object sender, DocumentChangedEventArgs e)
        {
            // Make sure if we are doing run to run regression that we use valid result indexes. 
            if (IsRunToRun)
            {
                if (_documentContainer.Document.MeasuredResults != null &&
                    _documentContainer.Document.MeasuredResults.Chromatograms.Count > 1)
                {
                    _originalResultsIndex = 0;
                    _originalResultsIndex = 1;
                }
                // Need at least 2 replicates to do run to run regression.
                else
                {
                    RTGraphController.GraphType = GraphTypeRT.score_to_run_regression;
                }
            }
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

        public int CurveCount { get { return GraphPanes.Sum(pane=>pane.CurveList.Count); } }

        internal IEnumerable<SummaryGraphPane> GraphPanes
        {
            get { return graphControl.MasterPane.PaneList.OfType<SummaryGraphPane>(); }
            set { graphControl.MasterPane.PaneList.Clear(); graphControl.MasterPane.PaneList.AddRange(value); }
        }

        public bool TryGetGraphPane<TPane>(out TPane pane) where TPane : class
        {
            pane = GraphPanes.FirstOrDefault() as TPane;
            return (pane != null);
        }

        protected override string GetPersistentString()
        {
            return base.GetPersistentString() + '|' + _controller.GetType().Name;
        }

        public IEnumerable<string> Categories
        {
            get { return GraphPanes.First().XAxis.Scale.TextLabels; }
        }

        public void UpdateUI(bool selectionChanged = true)
        {
            UpdateToolbar(_documentContainer.DocumentUI.Settings.MeasuredResults);
            UpdateGraph(true);
        }

        private void UpdateToolbar(MeasuredResults results)
        {
            if (!IsRunToRun)
            {
                if (!splitContainer.Panel1Collapsed)
                {
                    splitContainer.Panel1Collapsed = true;
                }
            }
            else
            {
                // Check to see if the list of files has changed.
                var listNames = new List<string>();
                foreach (var chromSet in results.Chromatograms)
                    listNames.Add(chromSet.Name);


                ResetResultsCombo(listNames, comboBoxTargetReplicates);
                var origIndex = ResetResultsCombo(listNames, comboOriginalReplicates);
                var targetIndex = StateProvider.SelectedResultsIndex;
                if (origIndex < 0)
                    origIndex = (targetIndex + 1) % results.Chromatograms.Count;
                _targetResultsIndex = targetIndex;
                _originalResultsIndex = origIndex;
                _dontUpdateForTargetSelectedIndex = true;
                comboBoxTargetReplicates.SelectedIndex = targetIndex;
                _dontUpdateOriginalSelectedIndex = true;
                comboOriginalReplicates.SelectedIndex = origIndex;
                
                // Show the toolbar after updating the files
                if (splitContainer.Panel1Collapsed)
                {
                    splitContainer.Panel1Collapsed = false;
                }
            }
        }

        private int ResetResultsCombo(List<string> listNames, ComboBox combo)
        {
            object selected = combo.SelectedItem;
            combo.Items.Clear();
            foreach (string name in listNames)
                combo.Items.Add(name);
            ComboHelper.AutoSizeDropDown(combo);
            return selected != null ? combo.Items.IndexOf(selected) : -1;
        }

        private void UpdateGraph(bool checkData)
        {
            // Only worry about updates, if the graph is visible
            // And make sure it is not disposed, since rendering happens on a timer
            if (!Visible || IsDisposed)
                return;

            // Avoid updating when document container and state provider are out of sync
            if (!ReferenceEquals(DocumentUIContainer.Document, StateProvider.SelectionDocument))
                return;
           
            // CONSIDER: Need a better guarantee that this ratio index matches the
            //           one in the sequence tree, but at least this will keep the UI
            //           from crashing with IndexOutOfBoundsException.
            var mods = DocumentUIContainer.DocumentUI.Settings.PeptideSettings.Modifications;
            _ratioIndex = Math.Min(_ratioIndex, mods.RatioInternalStandardTypes.Count - 1);

            // Only show ratios if document changes to have valid ratios
            if (AreaGraphController.AreaView == AreaNormalizeToView.area_ratio_view && !mods.HasHeavyModifications)
                AreaGraphController.AreaView = AreaNormalizeToView.none;

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
                pane.UpdateGraph(checkData);
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

        private bool graphControl_MouseDownEvent(ZedGraphControl sender, MouseEventArgs e)
        {
            var graphPane = GraphPaneFromPoint(e.Location);
            return null != graphPane && graphPane.HandleMouseDownEvent(sender, e);
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
                    GraphPanes = paneKeys.Select(key => graphController.CreateReplicatePane(key));
                    break;
                case GraphTypeSummary.peptide:
                    GraphPanes = paneKeys.Select(key => graphController.CreatePeptidePane(key));
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
                                if (hasPrecursors && hasProducts)
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
                    paneKeys = StateProvider.SelectionDocument.MoleculeTransitionGroups.Select(
                        group => new PaneKey(@group.TransitionGroup.LabelType)).Distinct().ToArray();
                }
            }
            paneKeys = paneKeys ?? new[] { PaneKey.DEFAULT };
            Array.Sort(paneKeys);
            return paneKeys;
        }


        private bool _dontUpdateForTargetSelectedIndex;
        private void toolStripComboBoxTargetReplicate_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_dontUpdateForTargetSelectedIndex)
                _dontUpdateForTargetSelectedIndex = false;
            else if (StateProvider.SelectedResultsIndex != comboBoxTargetReplicates.SelectedIndex)
                StateProvider.SelectedResultsIndex = comboBoxTargetReplicates.SelectedIndex;
        }

        private bool _dontUpdateOriginalSelectedIndex;
        private void toolStripComboBoxOriginalReplicate_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_dontUpdateOriginalSelectedIndex)
                _dontUpdateOriginalSelectedIndex = false;
            else
                SetResultIndexes(_targetResultsIndex,comboOriginalReplicates.SelectedIndex);
        }

        #region Functional Test Support

        public ComboBox RunToRunTargetReplicate { get { return comboBoxTargetReplicates; } }

        public ComboBox RunToRunOriginalReplicate { get { return comboOriginalReplicates;} }

        #endregion
        
        private void toolStrip_Resize(object sender, EventArgs e)
        {
            ResizeToolbar();
        }

        private void ResizeToolbar()
        {
            comboBoxTargetReplicates.Width = (splitContainer.Panel1.Bounds.Right - splitContainer.Panel1.Bounds.Left - 25 -
                                              label1.Width) / 2;
            comboOriginalReplicates.Width = comboBoxTargetReplicates.Width;
            comboOriginalReplicates.Left = comboBoxTargetReplicates.Left + comboBoxTargetReplicates.Width + 4;
        }
    }
}