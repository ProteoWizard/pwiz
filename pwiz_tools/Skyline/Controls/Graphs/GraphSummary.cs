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
using pwiz.Common.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using ZedGraph;

namespace pwiz.Skyline.Controls.Graphs
{
    public partial class GraphSummary : DockableFormEx, IUpdatable, IMultipleViewProvider
    {
        private const string FONT_FACE = "Arial"; // Not L10N
        private const int FONT_SIZE = 10;

        public static Color ColorSelected { get { return Color.Red; } }

        public static FontSpec CreateFontSpec(Color color)
        {
            return new FontSpec(FONT_FACE, FONT_SIZE, color, false, false, false, Color.Empty, null, FillType.None)
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
        }

        public class RTGraphView : IFormView {}
        public class AreaGraphView : IFormView {}

        public interface IStateProvider
        {
            SrmDocument SelectionDocument { get; }

            TreeNodeMS SelectedNode { get; }

            IdentityPath SelectedPath { get; set; }

            int SelectedResultsIndex { get; set; }

            GraphValues.IRetentionTimeTransformOp GetRetentionTimeTransformOperation();

            void ActivateSpectrum();

            void BuildGraphMenu(ZedGraphControl zedGraphControl, ContextMenuStrip menuStrip, Point mousPt, IController controller);
        }

        private class DefaultStateProvider : IStateProvider
        {
            public SrmDocument SelectionDocument { get { return null;}}
            public TreeNodeMS SelectedNode { get { return null; } }
            public IdentityPath SelectedPath { get { return IdentityPath.ROOT; } set { } }
            public void BuildGraphMenu(ZedGraphControl zedGraphControl, ContextMenuStrip menuStrip, Point mousePt, IController controller) { }
            public int SelectedResultsIndex { get; set; }
            public void ActivateSpectrum() {}
            public GraphValues.IRetentionTimeTransformOp GetRetentionTimeTransformOperation() {return null;}
        }

        private readonly IDocumentUIContainer _documentContainer;
        private readonly IStateProvider _stateProvider;
        private readonly IController _controller;

        private bool _activeLibrary;
        private int _resultsIndex;
        private int _ratioIndex;

        public GraphSummary(IDocumentUIContainer documentUIContainer, IController controller)
        {
            InitializeComponent();

            Icon = Resources.SkylineData;

            graphControl.MasterPane.Border.IsVisible = false;

            _controller = controller;
            _controller.GraphSummary = this;

            _documentContainer = documentUIContainer;
            _documentContainer.ListenUI(OnDocumentUIChanged);
            _stateProvider = documentUIContainer as IStateProvider ??
                             new DefaultStateProvider();

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
            get { return _resultsIndex; }
            set
            {
                if (_resultsIndex != value)
                {
                    _resultsIndex = value;

                    _controller.OnResultsIndexChanged();
                }
            }
        }

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
            UpdateGraph(true);
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
    }
}