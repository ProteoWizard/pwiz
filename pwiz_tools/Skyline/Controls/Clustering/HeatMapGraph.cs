/*
 * Original author: Ali Marsh <alimarsh .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * Copyright 2020 University of Washington - Seattle, WA
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
using pwiz.Common.Controls.Clustering;
using pwiz.Common.DataBinding.Clustering;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using ZedGraph;

namespace pwiz.Skyline.Controls.Clustering
{
    public partial class HeatMapGraph : DataboundGraph
    {
        private DendrogramScale _rowDendrogramScale;
        private DendrogramScale _columnDendrogramScale;
        private bool _showSelection = true;
        private AxisLabelScaler _xAxisLabelScaler;
        private AxisLabelScaler _yAxisLabelScaler;
        private readonly HeatMapCalculator _calculator;

        public HeatMapGraph()
        {
            InitializeComponent();
            InitializeDendrograms();
            _calculator = new HeatMapCalculator(this);
            var graphPane = zedGraphControl1.GraphPane;
            graphPane.Title.Text = Resources.RTLinearRegressionGraphPane_UpdateGraph_Calculating___;
            graphPane.XAxis.Title.IsVisible = false;
            graphPane.YAxis.Title.IsVisible = false;
            graphPane.Legend.IsVisible = false;
            graphPane.Margin.All = 0;
            graphPane.Border.IsVisible = false;

            graphPane.XAxis.MinorTic.Size = 0;
            graphPane.XAxis.MajorTic.IsOpposite = false;
            graphPane.XAxis.MajorTic.Size = 2;
            graphPane.YAxis.MinorTic.Size = 0;
            graphPane.YAxis.MajorTic.IsOpposite = false;
            graphPane.YAxis.MajorTic.Size = 2;

            graphPane.X2Axis.MinorTic.Size = 0;
            graphPane.X2Axis.MajorTic.Size = 0;
            graphPane.Y2Axis.MinorTic.Size = 0;
            graphPane.Y2Axis.MajorTic.Size = 0;

            graphPane.XAxis.Scale.FontSpec = GraphSummary.CreateFontSpec(Color.Black);
            graphPane.YAxis.Scale.FontSpec = GraphSummary.CreateFontSpec(Color.Black);
            graphPane.YAxis.Scale.FontSpec.Angle = 90;

            _xAxisLabelScaler = new AxisLabelScaler(graphPane, graphPane.XAxis)
            {
                IsRepeatRemovalAllowed = true
            };
            _yAxisLabelScaler = new AxisLabelScaler(graphPane, graphPane.YAxis)
            {
                IsRepeatRemovalAllowed = true
            };
        }

        public override ZedGraphControl GraphControl => zedGraphControl1;

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (SkylineWindow != null)
            {
                SkylineWindow.SequenceTree.AfterSelect += SequenceTree_OnAfterSelect;
                SkylineWindow.ComboResults.SelectedIndexChanged += ComboResults_OnSelectedIndexChanged;
            }
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            if (SkylineWindow != null)
            {
                SkylineWindow.SequenceTree.AfterSelect -= SequenceTree_OnAfterSelect;
                SkylineWindow.ComboResults.SelectedIndexChanged -= ComboResults_OnSelectedIndexChanged;
            }
            base.OnHandleDestroyed(e);
        }
        private void ComboResults_OnSelectedIndexChanged(object sender, EventArgs e)
        {
            if (sender == null || !ReferenceEquals(sender, SkylineWindow?.ComboResults))
            {
                return;
            }
            UpdateSelection();
        }

        private void SequenceTree_OnAfterSelect(object sender, TreeViewEventArgs e)
        {
            if (sender == null || !ReferenceEquals(sender, SkylineWindow?.SequenceTree))
            {
                return;
            }
            UpdateSelection();
        }

        public ClusterInput ClusterInput
        {
            get
            {
                return _calculator.Input;
            }
            set
            {
                _calculator.Input = value;
            }
        }


        public ClusterGraphResults GraphResults
        {
            get { return _calculator.Results; }
        }

        public override bool IsComplete
        {
            get
            {
                return base.IsComplete && _calculator.IsComplete;
            }
        }

        public void InitializeDendrograms()
        {
            // make visible
            zedGraphControl1.GraphPane.X2Axis.IsVisible = true;
            zedGraphControl1.GraphPane.Y2Axis.IsVisible = true;
            // create user defined axis and set to dendrogram
            zedGraphControl1.GraphPane.X2Axis.SetScale(new DendrogramScale(zedGraphControl1.GraphPane.X2Axis, DockStyle.Top));
            zedGraphControl1.GraphPane.Y2Axis.SetScale(new DendrogramScale(zedGraphControl1.GraphPane.Y2Axis, DockStyle.Right));

            _columnDendrogramScale = (DendrogramScale)zedGraphControl1.GraphPane.X2Axis.Scale;
            _rowDendrogramScale = (DendrogramScale)zedGraphControl1.GraphPane.Y2Axis.Scale;
            
        }

        public void UpdateGraph()
        {
            zedGraphControl1.GraphPane.Title.IsVisible = false;
            zedGraphControl1.GraphPane.CurveList.Clear();

            var dataSet = GraphResults;
            if (dataSet.ColumnGroups.Count == 1)
            {
                zedGraphControl1.GraphPane.XAxis.Title.Text =
                    TextUtil.SpaceSeparate(dataSet.ColumnGroups[0].Headers.Select(header => header.Caption));
            }
            else
            {
                zedGraphControl1.GraphPane.XAxis.Title.Text = string.Empty;
            }

            zedGraphControl1.GraphPane.YAxis.Title.Text =
                TextUtil.SpaceSeparate(dataSet.RowHeaders.Select(header => header.Caption));
            var points = new PointPairList();
            foreach (var point in dataSet.Points)
            {
                if (point.Color.HasValue)
                {
                    var pointPair = MakePointPair(point);
                    points.Add(pointPair);
                }
            }
            zedGraphControl1.GraphPane.CurveList.Add(new ClusteredHeatMapItem(string.Empty, points));

            zedGraphControl1.GraphPane.YAxis.Type = AxisType.Text;
            zedGraphControl1.GraphPane.YAxis.Scale.TextLabels = dataSet.RowHeaders.Select(header=>header.Caption).Reverse().ToArray();

            zedGraphControl1.GraphPane.XAxis.Type = AxisType.Text;
            zedGraphControl1.GraphPane.XAxis.Scale.TextLabels =
                dataSet.ColumnGroups.SelectMany(group => group.Headers.Select(header=>header.Caption)).ToArray();
            UpdateSelection();
            ScaleAxisLabels();
            zedGraphControl1.Invalidate();

            UpdateDendrograms();
        }

        private PointPair MakePointPair(ClusterGraphResults.Point point)
        {
            var dataSet = GraphResults;
            return new PointPair(point.ColumnIndex + 1, dataSet.RowCount - point.RowIndex)
            {
                Tag = point.Color
            };
        }

        public override void UpdateSelection()
        {
            if (GraphResults == null)
            {
                return;
            }
            if (!ShowSelection)
            {
                if (!zedGraphControl1.GraphPane.GraphObjList.Any())
                {
                    return;
                }
            }
            var selectedPath = SkylineWindow.SelectedPath;
            string selectedReplicate = null;
            if (SkylineWindow.SelectedResultsIndex >= 0)
            {
                selectedReplicate = SkylineWindow.DocumentUI.MeasuredResults
                    ?.Chromatograms[SkylineWindow.SelectedResultsIndex].Name;
            }

            zedGraphControl1.GraphPane.GraphObjList.Clear();
            if (ShowSelection)
            {
                var selectedPoints = GraphResults.Points.Where(p =>
                    Equals(p.IdentityPath, selectedPath) && Equals(p.ReplicateName, selectedReplicate)).ToList();
                foreach (var selectedPoint in selectedPoints)
                {
                    var pointPair = MakePointPair(selectedPoint);
                    var graphObj = new BoxObj(pointPair.X - .5, pointPair.Y + .5, 1, 1, Color.Black, Color.Transparent)
                    {
                        IsClippedToChartRect = true,
                        ZOrder = ZOrder.D_BehindAxis,

                    };
                    zedGraphControl1.GraphPane.GraphObjList.Add(graphObj);
                }
            }
            zedGraphControl1.Invalidate();
        }

        public List<DendrogramFormat> GetUpdatedColumnDendrograms()
        {
            if (!GraphResults.ColumnGroups.Any())
            {
                return null;
            }
            var columnDendrograms = new List<DendrogramFormat>();
            double xStart = .5;
            foreach (var group in GraphResults.ColumnGroups)
            {
                var locations = new List<KeyValuePair<double, double>>();
                var colors = new List<ImmutableList<Color>>();
                for (int i = 0; i < group.Headers.Count; i++)
                {
                    double x1 = xStart + i;
                    double x2 = xStart + i + 1;
                    locations.Add(new KeyValuePair<double, double>(x1, x2));
                    colors.Add(group.Headers[i].Colors);
                }
                columnDendrograms.Add(new DendrogramFormat(group.DendrogramData, locations, colors));
                xStart += group.Headers.Count;
            }

            return columnDendrograms;
        }

        public List<DendrogramFormat> GetUpdatedRowDendrograms()
        {
            if (GraphResults.RowDendrogramData == null)
            {
                return null;
            }
            var rowLocations = new List<KeyValuePair<double, double>>();
            var colors = new List<ImmutableList<Color>>();
            int rowCount = GraphResults.RowCount;
            for (int rowIndex = 0; rowIndex < rowCount; rowIndex++)
            {
                var y1 = rowCount + .5 - rowIndex;
                var y2 = rowCount - .5 - rowIndex;
                rowLocations.Add(new KeyValuePair<double, double>(y1, y2));
                colors.Add(GraphResults.RowHeaders[rowIndex].Colors);
            }
            return new List<DendrogramFormat>
                {new DendrogramFormat(GraphResults.RowDendrogramData, rowLocations, colors)};
        }


        public void UpdateDendrograms()
        {
            _columnDendrogramScale.Update(GetUpdatedColumnDendrograms());
            _rowDendrogramScale.Update(GetUpdatedRowDendrograms());
        }

        private void zedGraphControl1_Resize(object sender, EventArgs e)
        {
            ScaleAxisLabels();
        }

        private void ScaleAxisLabels()
        {
            if (_yAxisLabelScaler == null || _xAxisLabelScaler == null)
            {
                // It is possible for this method to be called before the HeatMapGraph constructor has finished
                return;
            }
            _yAxisLabelScaler.ScaleAxisLabels();
            zedGraphControl1.AxisChange();
            _xAxisLabelScaler.ScaleAxisLabels();
            zedGraphControl1.AxisChange();
        }

        public bool ShowXAxisLabels
        {
            get
            {
                return zedGraphControl1.GraphPane.XAxis.Scale.IsVisible;
            }
            set
            {
                zedGraphControl1.GraphPane.XAxis.Scale.IsVisible = value;
                zedGraphControl1.GraphPane.AxisChange();
                zedGraphControl1.Invalidate();
            }
        }

        public bool ShowYAxisLabels
        {
            get
            {
                return zedGraphControl1.GraphPane.YAxis.Scale.IsVisible;
            }
            set
            {
                zedGraphControl1.GraphPane.YAxis.Scale.IsVisible = value;
                zedGraphControl1.GraphPane.AxisChange();
                zedGraphControl1.Invalidate();
            }
        }

        public bool ShowSelection
        {
            get
            {
                return _showSelection;
            }
            set
            {
                _showSelection = value;
                UpdateSelection();
            }
        }

        private void zedGraphControl1_ContextMenuBuilder(ZedGraphControl sender, ContextMenuStrip menuStrip, Point mousePt, ZedGraphControl.ContextMenuObjectState objState)
        {
            ZedGraphHelper.BuildContextMenu(sender, menuStrip, true);
            menuStrip.Items.Insert(0, new ToolStripSeparator());
            menuStrip.Items.Insert(0, new ToolStripMenuItem(Resources.HierarchicalClusterGraph_zedGraphControl1_ContextMenuBuilder_Y_Axis_Labels, null, ShowYAxisLabelsOnClick)
            {
                Checked = ShowYAxisLabels
            });
            menuStrip.Items.Insert(0, new ToolStripMenuItem(Resources.HierarchicalClusterGraph_zedGraphControl1_ContextMenuBuilder_X_Axis_Labels, null, ShowXAxisLabelsOnClick)
            {
                Checked = ShowXAxisLabels
            });
            menuStrip.Items.Insert(0, new ToolStripMenuItem(Resources.HierarchicalClusterGraph_zedGraphControl1_ContextMenuBuilder_Show_Selection, null, ShowSelectionOnClick)
            {
                Checked = ShowSelection
            });
            menuStrip.Items.Insert(0, new ToolStripSeparator());
            menuStrip.Items.Insert(0, new ToolStripMenuItem(Resources.HeatMapGraph_zedGraphControl1_ContextMenuBuilder_Refresh, null, (o, args)=>RefreshData()));
            var pointObject = PointFromMousePoint(mousePt);
            if (pointObject?.ReplicateName != null || pointObject?.IdentityPath != null)
            {
                menuStrip.Items.Insert(0, new ToolStripMenuItem(Resources.HierarchicalClusterGraph_zedGraphControl1_ContextMenuBuilder_Select, null, (o, args)=>SelectPoint(pointObject)));
            }
        }

        private void ShowYAxisLabelsOnClick(object sender, EventArgs args)
        {
            ShowYAxisLabels = !ShowYAxisLabels;
        }

        private void ShowXAxisLabelsOnClick(object sender, EventArgs args)
        {
            ShowXAxisLabels = !ShowXAxisLabels;
        }

        private void ShowSelectionOnClick(object sender, EventArgs args)
        {
            ShowSelection = !ShowSelection;
        }


        private ClusterGraphResults.Point PointFromMousePoint(Point mousePoint)
        {
            if (GraphResults == null)
            {
                return null;
            }
            var graphPane = zedGraphControl1.GraphPane;
            graphPane.ReverseTransform(new PointF(mousePoint.X, mousePoint.Y), out double x, out double y);
            if (x < graphPane.XAxis.Scale.Min || x > graphPane.XAxis.Scale.Max)
            {
                return null;
            }

            if (y < graphPane.YAxis.Scale.Min || y > graphPane.YAxis.Scale.Max)
            {
                return null;
            }

            var columnIndex = (int)Math.Round(x - 1);
            var rowIndex = (int)Math.Round(GraphResults.RowCount - y);
            return GraphResults.Points.FirstOrDefault(p =>
                p.ColumnIndex == columnIndex && p.RowIndex == rowIndex);
        }

        private void SelectPoint(ClusterGraphResults.Point point)
        {
            SkylineWindow?.SelectPathAndReplicate(point?.IdentityPath, point?.ReplicateName);
        }

        public override void RefreshData()
        {
            ClusterInput = DataboundGridControl?.CreateClusterInput() ?? ClusterInput;
            UpdateTitle(Resources.HeatMapGraph_RefreshData_Heat_Map);
        }
        private class HeatMapCalculator : BoundGraphDataCalculator<ClusterInput, ClusterGraphResults>
        {
            public HeatMapCalculator(HeatMapGraph heatMapGraph) : base(CancellationToken.None, heatMapGraph.zedGraphControl1)
            {
                HeatMapGraph = heatMapGraph;
            }

            public HeatMapGraph HeatMapGraph { get; }


            protected override ClusterGraphResults CalculateDataBoundResults(ClusterInput input, CancellationToken cancellationToken)
            {
                var resultsTuple = input.GetClusterResultsTuple(GetProgressHandler(cancellationToken));
                if (resultsTuple == null)
                {
                    return null;
                }

                var finalColorScheme = ReportColorScheme.FromClusteredResults(cancellationToken, resultsTuple.Item2);
                var finalResults = input.GetClusterGraphResults(cancellationToken, resultsTuple.Item2, finalColorScheme);
                return finalResults;
            }

            protected override void ResultsAvailable()
            {
                HeatMapGraph.UpdateGraph();
            }
        }
    }
}
