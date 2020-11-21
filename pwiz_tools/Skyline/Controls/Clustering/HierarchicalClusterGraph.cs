using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.DataAnalysis.Clustering;
using pwiz.Common.DataBinding.Clustering;
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
                double xGroupStart = 0;
                foreach (var dataFrameGroup in dataSet.DataFrameGroups)
                {
                    var zScoreLists = dataFrameGroup.Select(frame => frame.GetZScores(iRow).ToList()).ToList();
                    int frameCount = dataFrameGroup.Count;
                    int colCountInGroup = dataFrameGroup.First().ColumnHeaders.Count;

                    // Add points for the zScores of all of the columns
                    // If there is more than one data frame in this group, then the points will 
                    // be plotted with fractional x values so that all of the group's points fit within
                    // a single integer
                    for (int iColInGroup = 0; iColInGroup < colCountInGroup; iColInGroup++)
                    {
                        for (int iFrame = 0; iFrame < frameCount; iFrame++)
                        {
                            double? zScore = zScoreLists[iFrame][iColInGroup];
                            double x = xGroupStart + iColInGroup + iFrame / (double)frameCount;
                            points.Add(new PointPair(x, iRow, zScore.HasValue ? zScore.Value : PointPairBase.Missing));
                        }
                    }

                    xGroupStart += colCountInGroup;
                }
            }
            zedGraphControl1.GraphPane.CurveList.Add(new ClusteredHeatMapItem("Points", points));

            zedGraphControl1.GraphPane.YAxis.Type = AxisType.Text;
            zedGraphControl1.GraphPane.YAxis.Scale.TextLabels = dataSet.RowLabels.ToArray();

            zedGraphControl1.GraphPane.XAxis.Type = AxisType.Text;
            zedGraphControl1.GraphPane.XAxis.Scale.TextLabels =
                dataSet.DataFrameGroups.SelectMany(group => group[0].ColumnHeaders).ToArray();
            AxisLabelScaler scaler = new AxisLabelScaler(zedGraphControl1.GraphPane);
            scaler.ScaleAxisLabels();
            zedGraphControl1.AxisChange();
            zedGraphControl1.Invalidate();

            UpdateDendrograms();
        }

        public void UpdateColumnDendrograms()
        {
            if (Results.ColumnGroupDendrograms == null)
            {
                splitContainerHorizontal.Panel1Collapsed = true;
                return;
            }

            var datas = new List<KeyValuePair<DendrogramData, ImmutableList<double>>>();
            double xStart = 0;
            for (int iGroup = 0; iGroup < Results.DataSet.DataFrameGroups.Count; iGroup++)
            {
                var group = Results.DataSet.DataFrameGroups[iGroup];
                var locations = Enumerable.Range(0, group[0].ColumnHeaders.Count).Select(i =>
                        (double) zedGraphControl1.GraphPane.GeneralTransform(xStart + i, 0.0, CoordType.AxisXYScale).X)
                    .ToList();
                datas.Add(new KeyValuePair<DendrogramData, ImmutableList<double>>(
                    Results.ColumnGroupDendrograms[iGroup], ImmutableList.ValueOf(locations)));
                xStart += group[0].ColumnHeaders.Count;
            }
           
            columnDendrogram.SetDendrogramDatas(datas);
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
