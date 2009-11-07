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
using System.Drawing;
using System.Windows.Forms;
using DigitalRune.Windows.Docking;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using ZedGraph;

namespace pwiz.Skyline.Controls.Graphs
{
    public enum GraphTypeArea { replicate, peptide }

    public partial class GraphPeakArea : DockableForm, IGraphContainer
    {
        public static GraphTypeArea GraphType
        {
            get
            {
                try
                {
                    return (GraphTypeArea)Enum.Parse(typeof(GraphTypeArea),
                                                     Settings.Default.AreaGraphType);
                }
                catch (Exception)
                {
                    return GraphTypeArea.replicate;
                }
            }
        }

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

        public interface IStateProvider
        {
            TreeNode SelectedNode { get; }

            IdentityPath SelectedPath { get; set; }

            int SelectedResultsIndex { get; set; }

            void BuildAreaGraphMenu(ContextMenuStrip menuStrip);
        }

        private class DefaultStateProvider : IStateProvider
        {
            public TreeNode SelectedNode { get { return null; } }
            public IdentityPath SelectedPath { get { return IdentityPath.ROOT; } set {} }
            public void BuildAreaGraphMenu(ContextMenuStrip menuStrip) { }
            public int SelectedResultsIndex { get; set; }
        }

        private readonly IDocumentUIContainer _documentContainer;
        private readonly IStateProvider _stateProvider;

        private int _resultsIndex;

        public GraphPeakArea(IDocumentUIContainer documentUIContainer)
        {
            InitializeComponent();

            graphControl.MasterPane[0] = new AreaReplicateGraphPane
                                             {
                                                 GraphPeakArea = this
                                             };
            graphControl.MasterPane.Border.IsVisible = false;

            _documentContainer = documentUIContainer;
            _documentContainer.ListenUI(OnDocumentUIChanged);
            _stateProvider = documentUIContainer as IStateProvider ??
                             new DefaultStateProvider();
        }

        public int ResultsIndex
        {
            get { return _resultsIndex; }
            set
            {
                if (_resultsIndex != value)
                {
                    _resultsIndex = value;

                    if (GraphPane is AreaReplicateGraphPane /* || !Settings.Default.AreaAverageReplicates */)
                        UpdateGraph();
                }
            }
        }

        public IDocumentUIContainer DocumentUIContainer { get { return _documentContainer; }}

        public void OnDocumentUIChanged(object sender, DocumentChangedEventArgs e)
        {
            UpdateGraph();
        }

        private void GraphPeakArea_VisibleChanged(object sender, EventArgs e)
        {
            UpdateGraph();
        }

        private void GraphPeakArea_KeyDown(object sender, KeyEventArgs e)
        {
            if (GraphPane.HandleKeyDownEvent(sender, e))
                return;

            switch (e.KeyCode)
            {
                case Keys.Escape:
                    _documentContainer.FocusDocument();
                    break;
                case Keys.D2:
                    if (e.Alt)
                        Hide();
                    break;
                case Keys.F7:
                    if (!e.Alt && !(e.Shift && e.Control))
                    {
                        if (e.Control)
                            Settings.Default.AreaGraphType = GraphTypeArea.peptide.ToString();
                        else
                            Settings.Default.AreaGraphType = GraphTypeArea.replicate.ToString();
                        UpdateGraph();
                    }
                    break;
            }
        }

        internal AreaGraphPane GraphPane { get { return graphControl.MasterPane[0] as AreaGraphPane; } }

        public void UpdateGraph()
        {
            UpdateGraph(true);
        }

        private void UpdateGraph(bool checkData)
        {
            // Only worry about updates, if the graph is visible
            // And make sure it is not disposed, since rendering happens on a timer
            if (!Visible || IsDisposed)
                return;

            SetGraphType(GraphType);

            var graphPane = GraphPane;
            if (graphPane != null)
            {
                graphPane.UpdateGraph(checkData);
                graphControl.Invalidate();
            }
        }

        private void graphControl_ContextMenuBuilder(ZedGraphControl sender, ContextMenuStrip menuStrip, Point mousePt, ZedGraphControl.ContextMenuObjectState objState)
        {
            _stateProvider.BuildAreaGraphMenu(menuStrip);
        }

        private bool graphControl_MouseMoveEvent(ZedGraphControl sender, MouseEventArgs e)
        {
            return GraphPane.HandleMouseMoveEvent(sender, e);
        }

        private bool graphControl_MouseDownEvent(ZedGraphControl sender, MouseEventArgs e)
        {
            return GraphPane.HandleMouseDownEvent(sender, e);
        }

        public IStateProvider StateProvider { get { return _stateProvider; } }

        public void SetGraphType(GraphTypeArea type)
        {
            switch (type)
            {
                case GraphTypeArea.replicate:
                    if (!(GraphPane is AreaReplicateGraphPane))
                        graphControl.MasterPane[0] = new AreaReplicateGraphPane { GraphPeakArea = this };
                    break;
//                case GraphTypeArea.peptide:
//                    if (!(GraphPane is AreaPeptideGraphPane))
//                        graphControl.MasterPane[0] = new AreaPeptideGraphPane { GraphPeakArea = this };
//                    break;
            }

            using (Graphics g = CreateGraphics())
            {
                graphControl.MasterPane.DoLayout(g);
            }
        }

        public void LockYAxis(bool lockY)
        {
        }

        protected override void OnClosed(EventArgs e)
        {
            _documentContainer.UnlistenUI(OnDocumentUIChanged);
        }

        private void GraphPeakArea_Resize(object sender, EventArgs e)
        {
            // Apparently on Windows 7, a resize event may occur during InitializeComponent
            if (GraphPane != null)
                GraphPane.HandleResizeEvent();
        }
    }
}