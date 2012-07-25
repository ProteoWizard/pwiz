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
using System.Windows.Forms;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using ZedGraph;

namespace pwiz.Skyline.Controls.Graphs
{
    public partial class GraphSummary : DockableFormEx, IUpdatable
    {
        private const string FONT_FACE = "Arial";
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
        }

        public interface IStateProvider
        {
            TreeNodeMS SelectedNode { get; }

            IdentityPath SelectedPath { get; set; }

            int SelectedResultsIndex { get; set; }

            int AlignToReplicate { get; set; }

            void ActivateSpectrum();

            void BuildGraphMenu(ZedGraphControl zedGraphControl, ContextMenuStrip menuStrip, Point mousPt, IController controller);
        }

        private class DefaultStateProvider : IStateProvider
        {
            public TreeNodeMS SelectedNode { get { return null; } }
            public IdentityPath SelectedPath { get { return IdentityPath.ROOT; } set { } }
            public void BuildGraphMenu(ZedGraphControl zedGraphControl, ContextMenuStrip menuStrip, Point mousePt, IController controller) { }
            public int SelectedResultsIndex { get; set; }
            public void ActivateSpectrum() {}
            public int AlignToReplicate { get; set; }
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
            if (GraphPane.HandleKeyDownEvent(sender, e))
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

        public int CurveCount { get { return GraphPane.CurveList.Count; } }

        internal SummaryGraphPane GraphPane
        {
            get { return graphControl.MasterPane[0] as SummaryGraphPane; }
            set { graphControl.MasterPane[0] = value; }
        }

        protected override string GetPersistentString()
        {
            return base.GetPersistentString() + '|' + _controller.GetType().Name;
        }

        public IEnumerable<string> Categories
        {
            get { return GraphPane.XAxis.Scale.TextLabels; }
        }

        public void UpdateUI()
        {
            UpdateGraph(true);
        }

        private void UpdateGraph(bool checkData)
        {
            // Only worry about updates, if the graph is visible
            // And make sure it is not disposed, since rendering happens on a timer
            if (!Visible || IsDisposed)
                return;

            // CONSIDER: Need a better guarantee that this ratio index matches the
            //           one in the sequence tree, but at least this will keep the UI
            //           from crashing with IndexOutOfBoundsException.
            var mods = DocumentUIContainer.DocumentUI.Settings.PeptideSettings.Modifications;
            _ratioIndex = Math.Min(_ratioIndex, mods.InternalStandardTypes.Count - 1);

            // Only show ratios if document changes to have valid ratios
            if (AreaGraphController.AreaView == AreaNormalizeToView.area_ratio_view && !mods.HasHeavyModifications)
                AreaGraphController.AreaView = AreaNormalizeToView.none;

            var graphPaneCurrent = GraphPane;
            _controller.OnUpdateGraph();
            var graphPane = GraphPane;

            if (graphPaneCurrent != graphPane)
            {
                // Release any necessary resources from the old pane
                var disposable = graphPaneCurrent as IDisposable;
                if (disposable != null)
                    disposable.Dispose();

                // Layout the new pane
                using (Graphics g = CreateGraphics())
                {
                    graphControl.MasterPane.DoLayout(g);
                }                
            }

            if (graphPane != null)
            {
                graphPane.UpdateGraph(checkData);
                graphControl.Invalidate();
            }
        }

        private void graphControl_ContextMenuBuilder(ZedGraphControl sender, ContextMenuStrip menuStrip, Point mousePt, ZedGraphControl.ContextMenuObjectState objState)
        {
            _stateProvider.BuildGraphMenu(sender, menuStrip, mousePt, _controller);
        }

        private bool graphControl_MouseMoveEvent(ZedGraphControl sender, MouseEventArgs e)
        {
            if (e.Button == sender.PanButtons && ModifierKeys == sender.PanModifierKeys)
                GraphPane.EnsureYMin();
            return GraphPane.HandleMouseMoveEvent(sender, e);
        }

        private bool graphControl_MouseDownEvent(ZedGraphControl sender, MouseEventArgs e)
        {
            return GraphPane.HandleMouseDownEvent(sender, e);
        }

        private void graphControl_ZoomEvent(ZedGraphControl sender, ZoomState oldState, ZoomState newState)
        {
            GraphPane.EnsureYMin();
        }

        protected override void OnClosed(EventArgs e)
        {
            _documentContainer.UnlistenUI(OnDocumentUIChanged);
        }

        private void GraphSummary_Resize(object sender, EventArgs e)
        {
            // Apparently on Windows 7, a resize event may occur during InitializeComponent
            if (GraphPane != null)
                GraphPane.HandleResizeEvent();
        }
    }
}