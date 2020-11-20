using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.DataAnalysis.Clustering;
using pwiz.Common.DataBinding.Clustering;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Util;
using ZedGraph;

namespace pwiz.Skyline.Controls.Clustering
{
    public partial class HierarchicalClusterGraph : DockableFormEx
    {
        private ClusterResults _results;
        public HierarchicalClusterGraph()
        {
            InitializeComponent();
        }

        public ClusterResults Results
        {
            get { return _results; }
            set
            {
                _results = value;
                UpdateGraph();
            }
        }

        public void UpdateGraph()
        {
            zedGraphControl1.GraphPane.CurveList.Clear();
            zedGraphControl1.GraphPane.GraphObjList.Clear();

            var dataSet = Results.DataSet;
            var points = new PointPairList();
            for (int iRow = 0; iRow < dataSet.RowCount; iRow++)
            {
                int iCol = 0;
                foreach (var dataFrameGroup in dataSet.DataFrameGroups)
                {
                    var zScoreLists = dataFrameGroup.Select(frame => frame.GetZScores(iRow).ToList()).ToList();
                    var zScores = Enumerable.Range(0, dataFrameGroup.First().ColumnHeaders.Count)
                        .SelectMany(i => zScoreLists.Select(list => list[i]));
                    foreach (var zScore in zScores)
                    {
                        if (zScore.HasValue)
                        {
                            points.Add(new PointPair(iCol, iRow, zScore.Value));
                        }

                        iCol++;
                    }
                }
            }
            zedGraphControl1.GraphPane.CurveList.Add(new ClusteredHeatMapItem("Points", points));

            zedGraphControl1.GraphPane.YAxis.Type = AxisType.Text;
            zedGraphControl1.GraphPane.YAxis.Scale.TextLabels = dataSet.RowLabels.ToArray();
            AxisLabelScaler scaler = new AxisLabelScaler(zedGraphControl1.GraphPane);
            scaler.ScaleAxisLabels();
            zedGraphControl1.AxisChange();
            zedGraphControl1.Invalidate();

            UpdateDendrograms();
        }

        public void UpdateColumnDendrograms()
        {
            // TODO
            splitContainerHorizontal.Panel1Collapsed = true;
        }

        public void UpdateDendrograms()
        {
            UpdateColumnDendrograms();
            int rowDendrogramTop =
                splitContainerHorizontal.Panel1Collapsed ? 0 : splitContainerHorizontal.Panel1.Height;
            rowDendrogram.Bounds = new Rectangle(0, rowDendrogramTop, splitContainerVertical.Panel1.Width, splitContainerVertical.Panel1.Height - rowDendrogramTop);
            if (Results.RowDendrogram == null)
            {
                splitContainerVertical.Panel1Collapsed = true;
            }
            else
            {
                splitContainerVertical.Panel1Collapsed = false;
                int rowCount = Results.DataSet.RowCount;
                var rowLocations = ImmutableList.ValueOf(Enumerable.Range(0, rowCount).Select(
                    rowIndex =>
                        (double) zedGraphControl1.GraphPane
                            .GeneralTransform(0.0, rowIndex,
                                CoordType.AxisXYScale).Y));

                rowDendrogram.SetDendrogramDatas(new[] { new KeyValuePair<DendrogramData, ImmutableList<double>>(Results.RowDendrogram, rowLocations), });
            }

        }

        private void zedGraphControl1_ZoomEvent(ZedGraphControl sender, ZoomState oldState, ZoomState newState, System.Drawing.PointF mousePosition)
        {
            UpdateDendrograms();
        }

        private void zedGraphControl1_Resize(object sender, EventArgs e)
        {
            UpdateDendrograms();
        }
    }
}
