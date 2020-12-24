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
using System.Windows.Forms;
using pwiz.Common.Collections;
using pwiz.Common.Controls.Clustering;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using ZedGraph;

namespace pwiz.Skyline.Controls.Clustering
{
    public partial class HierarchicalClusterGraph : DockableFormEx
    {
        private ClusterGraphResults _graphResults;
        private DendrogramScale _rowDendrogramScale;
        private DendrogramScale _columnDendrogramScale;
        public HierarchicalClusterGraph()
        {
            InitializeComponent();
            InitializeDendrograms();
            zedGraphControl1.GraphPane.Title.IsVisible = false;
            zedGraphControl1.GraphPane.XAxis.Title.IsVisible = false;
            zedGraphControl1.GraphPane.YAxis.Title.IsVisible = false;
            zedGraphControl1.GraphPane.Legend.IsVisible = false;
            zedGraphControl1.GraphPane.Margin.All = 0;
            zedGraphControl1.GraphPane.Border.IsVisible = false;
        }

        public SkylineWindow SkylineWindow { get; set; }

        public ClusterGraphResults GraphResults
        {
            get { return _graphResults; }
            set
            {
                _graphResults = value;
                UpdateGraph();
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
            zedGraphControl1.GraphPane.CurveList.Clear();
            zedGraphControl1.GraphPane.GraphObjList.Clear();

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
                    var pointPair = new PointPair(point.ColumnIndex + 1, dataSet.RowCount - point.RowIndex)
                    {
                        Tag = point.Color
                    };
                    points.Add(pointPair);
                }
            }

            zedGraphControl1.GraphPane.CurveList.Add(new ClusteredHeatMapItem(string.Empty, points));

            zedGraphControl1.GraphPane.YAxis.Type = AxisType.Text;
            zedGraphControl1.GraphPane.YAxis.Scale.TextLabels = dataSet.RowHeaders.Select(header=>header.Caption).Reverse().ToArray();

            zedGraphControl1.GraphPane.XAxis.Type = AxisType.Text;
            zedGraphControl1.GraphPane.XAxis.Scale.TextLabels =
                dataSet.ColumnGroups.SelectMany(group => group.Headers.Select(header=>header.Caption)).ToArray();
            AxisLabelScaler scaler = new AxisLabelScaler(zedGraphControl1.GraphPane);
            scaler.ScaleAxisLabels();
            zedGraphControl1.AxisChange();
            zedGraphControl1.Invalidate();

            UpdateDendrograms();
        }

        public List<DendrogramFormat> GetUpdatedColumnDendrograms()
        {
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
            int rowCount = GraphResults.RowCount;
            var rowLocations = new List<KeyValuePair<double, double>>();
            var colors = new List<ImmutableList<Color>>();
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
            zedGraphControl1.GraphPane.Draw(zedGraphControl1.CreateGraphics());
            //UpdateDendrograms();
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

        private void zedGraphControl1_ContextMenuBuilder(ZedGraphControl sender, ContextMenuStrip menuStrip, Point mousePt, ZedGraphControl.ContextMenuObjectState objState)
        {
            ZedGraphHelper.BuildContextMenu(sender, menuStrip, true);
            menuStrip.Items.Add(new ToolStripMenuItem(Resources.HierarchicalClusterGraph_zedGraphControl1_ContextMenuBuilder_X_Axis_Labels, null, ShowXAxisLabelsOnClick)
            {
                Checked = ShowXAxisLabels
            });
            menuStrip.Items.Add(new ToolStripMenuItem(Resources.HierarchicalClusterGraph_zedGraphControl1_ContextMenuBuilder_Y_Axis_Labels, null, ShowYAxisLabelsOnClick)
            {
                Checked = ShowYAxisLabels
            });
            var pointObject = PointFromMousePoint(mousePt);
            if (pointObject?.ReplicateName != null || pointObject?.IdentityPath != null)
            {
                menuStrip.Items.Insert(0, new ToolStripSeparator());
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


        private ClusterGraphResults.Point PointFromMousePoint(Point mousePoint)
        {
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
            var rowIndex = (int)Math.Round(_graphResults.RowCount - y);
            return _graphResults.Points.FirstOrDefault(p =>
                p.ColumnIndex == columnIndex && p.RowIndex == rowIndex);
        }

        private void SelectPoint(ClusterGraphResults.Point point)
        {
            if (point == null || SkylineWindow == null)
            {
                return;
            }
            if (point.ReplicateName != null)
            {
                var replicateIndex = SkylineWindow?.DocumentUI.MeasuredResults?.Chromatograms
                    .IndexOf(c => c.Name == point.ReplicateName);
                if (replicateIndex.HasValue && replicateIndex.Value >= 0)
                {
                    SkylineWindow.SelectedResultsIndex = replicateIndex.Value;
                }
            }

            if (point.IdentityPath != null)
            {
                try
                {
                    SkylineWindow.SelectedPath = point.IdentityPath;
                }
                catch (IdentityNotFoundException)
                {
                    // Ignore
                }
            }
        }
    }
}
