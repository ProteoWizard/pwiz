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
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using ZedGraph;

namespace pwiz.Skyline.Controls.Graphs
{
    public enum GraphTypeRT { regression, replicate, schedule }

    public partial class GraphRetentionTime : DockableForm, IGraphContainer
    {
        public static GraphTypeRT GraphType
        {
            get
            {
                try
                {
                    return (GraphTypeRT)Enum.Parse(typeof(GraphTypeRT),
                                                   Settings.Default.RTGraphType);
                }
                catch (Exception)
                {
                    return GraphTypeRT.regression;
                }
            }
        }

        private const string FONT_FACE = "Arial";
        private const int FONT_SIZE = 10;

        public static readonly Color COLOR_REFINED = Color.DarkBlue;
        public static readonly Color COLOR_LINE_REFINED = Color.Black;
        public static readonly Color COLOR_LINE_PREDICT = Color.DarkGray;
        public static readonly Color COLOR_OUTLIERS = Color.BlueViolet;
        public static readonly Color COLOR_LINE_ALL = Color.BlueViolet;

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

            void BuildRTGraphMenu(ContextMenuStrip menuStrip, bool showDelete, bool showDeleteOutliers);
        }

        private class DefaultStateProvider : IStateProvider
        {
            public TreeNode SelectedNode { get { return null; } }
            public IdentityPath SelectedPath { get { return IdentityPath.ROOT; } set {} }
            public void BuildRTGraphMenu(ContextMenuStrip menuStrip, bool show1, bool show2) { }
            public int SelectedResultsIndex { get; set; }
        }

        public static double OutThreshold { get { return Settings.Default.RTResidualRThreshold; }}

        private readonly IDocumentUIContainer _documentContainer;
        private readonly IStateProvider _stateProvider;

        private int _resultsIndex;

        public GraphRetentionTime(IDocumentUIContainer documentUIContainer)
        {
            InitializeComponent();

            graphControl.MasterPane[0] = new RTLinearRegressionGraphPane
                                             {
                                                 GraphRetentionTime = this
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

                    if (GraphPane is RTReplicateGraphPane || !Settings.Default.RTAverageReplicates)
                        UpdateGraph();
                }
            }
        }

        public PeptideDocNode[] Outliers
        {
            get
            {
                return GraphPane.Outliers;
            }
        }

        public IDocumentUIContainer DocumentUIContainer { get { return _documentContainer; }}

        public RetentionTimeRegression RegressionRefined
        {
            get
            {
                return GraphPane.RegressionRefined;
            }
        }

        public void OnDocumentUIChanged(object sender, DocumentChangedEventArgs e)
        {
            UpdateGraph();
        }

        private void GraphRetentionTime_VisibleChanged(object sender, EventArgs e)
        {
            UpdateGraph();
        }

        private void GraphRetentionTime_KeyDown(object sender, KeyEventArgs e)
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
                case Keys.F8:
                    if (!e.Alt && !(e.Shift && e.Control))
                    {
                        if (e.Shift)
                            Settings.Default.RTGraphType = GraphTypeRT.regression.ToString();
                        else if (e.Control)
                            Settings.Default.RTGraphType = GraphTypeRT.schedule.ToString();
                        else
                            Settings.Default.RTGraphType = GraphTypeRT.replicate.ToString();
                        UpdateGraph();
                    }
                    break;
            }
        }

        internal RTGraphPane GraphPane { get { return graphControl.MasterPane[0] as RTGraphPane; } }

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
            bool showDelete = GraphPane.AllowDeletePoint(new PointF(mousePt.X, mousePt.Y));
            bool showDeleteOutliers = GraphPane.HasOutliers;
            _stateProvider.BuildRTGraphMenu(menuStrip, showDelete, showDeleteOutliers);
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

        public void SetGraphType(GraphTypeRT type)
        {
            switch (type)
            {
                case GraphTypeRT.regression:
                    if (!(GraphPane is RTLinearRegressionGraphPane))
                        graphControl.MasterPane[0] = new RTLinearRegressionGraphPane { GraphRetentionTime = this };
                    break;
                case GraphTypeRT.replicate:
                    if (!(GraphPane is RTReplicateGraphPane))
                        graphControl.MasterPane[0] = new RTReplicateGraphPane { GraphRetentionTime = this };
                    break;
                case GraphTypeRT.schedule:
                    if (!(GraphPane is RTScheduleGraphPane))
                        graphControl.MasterPane[0] = new RTScheduleGraphPane { GraphRetentionTime = this };
                    break;
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

        private void GraphRetentionTime_Resize(object sender, EventArgs e)
        {
            // Apparently on Windows 7, a resize event may occur during InitializeComponent
            if (GraphPane != null)
                GraphPane.HandleResizeEvent();
        }
    }
}