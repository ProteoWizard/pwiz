using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.Collections;
using pwiz.Common.Controls.Clustering;
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
            zedGraphControl1.GraphPane.X2Axis.Type = AxisType.UserDefined;
            zedGraphControl1.GraphPane.Y2Axis.Type = AxisType.UserDefined;
            zedGraphControl1.GraphPane.X2Axis.SetUserDefinedScale(new DendrogramScale(zedGraphControl1.GraphPane.X2Axis, DockStyle.Top));
            zedGraphControl1.GraphPane.Y2Axis.SetUserDefinedScale(new DendrogramScale(zedGraphControl1.GraphPane.Y2Axis, DockStyle.Right));

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
                points.Add(new PointPair(point.ColumnIndex + 1, dataSet.RowCount - point.RowIndex)
                {
                    Tag = point.Color
                });
            }
            zedGraphControl1.GraphPane.CurveList.Add(new ClusteredHeatMapItem("Points", points));

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
                    double x1 = (double)zedGraphControl1.GraphPane
                        .GeneralTransform(xStart + i, 0.0, CoordType.AxisXYScale).X;
                    double x2 = zedGraphControl1.GraphPane
                        .GeneralTransform(xStart + i + 1, 0.0, CoordType.AxisXYScale).X;
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
                var y1 = zedGraphControl1.GraphPane.GeneralTransform(0.0, rowCount + .5 - rowIndex,
                    CoordType.AxisXYScale).Y;
                var y2 = zedGraphControl1.GraphPane.GeneralTransform(0.0, rowCount - .5 - rowIndex,
                    CoordType.AxisXYScale).Y;
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

        private void zedGraphControl1_ZoomEvent(ZedGraphControl sender, ZoomState oldState, ZoomState newState, System.Drawing.PointF mousePosition)
        {
            UpdateDendrograms();
        }

        public class HeaderInfo
        {
            public string Caption { get; private set; }
            public ImmutableList<Color> Colors { get; private set; }
        }

        private void splitContainerVertical_SplitterMoved(object sender, SplitterEventArgs e)
        {
            UpdateDendrograms();
        }

        private void zedGraphControl1_Resize(object sender, EventArgs e)
        {
            zedGraphControl1.GraphPane.Draw(zedGraphControl1.CreateGraphics());
            UpdateDendrograms();
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
            menuStrip.Items.Add(new ToolStripMenuItem("X-Axis Labels", null, ShowXAxisLabelsOnClick)
            {
                Checked = ShowXAxisLabels
            });
            menuStrip.Items.Add(new ToolStripMenuItem("Y-Axis Labels", null, ShowYAxisLabelsOnClick)
            {
                Checked = ShowYAxisLabels
            });
        }

        private void ShowYAxisLabelsOnClick(object sender, EventArgs args)
        {
            ShowYAxisLabels = !ShowYAxisLabels;
        }

        private void ShowXAxisLabelsOnClick(object sender, EventArgs args)
        {
            ShowXAxisLabels = !ShowXAxisLabels;
        }
    }
}
