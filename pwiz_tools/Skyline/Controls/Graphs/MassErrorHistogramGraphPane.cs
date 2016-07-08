/*
 * Original author: Alex MacLean <alexmaclean2000 .at. gmail.com>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2016 University of Washington - Seattle, WA
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
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using ZedGraph;

namespace pwiz.Skyline.Controls.Graphs
{
    class MassErrorHistogramGraphPane : SummaryGraphPane
    {
        public static ReplicateDisplay ShowReplicate
        {
            get
            {
                return Helpers.ParseEnum(Settings.Default.ShowRegressionReplicateEnum, ReplicateDisplay.all);
            }
        }

        private GraphData Data { get; set; }

        public MassErrorHistogramGraphPane(GraphSummary graphSummary)
            : base(graphSummary)
        {
        }

        public void Update(SrmDocument document, int resultIndex, PointsTypeMassError pointsType)
        {
            var bestResults = ShowReplicate == ReplicateDisplay.best;
            Data = new GraphData(document, Data, resultIndex, bestResults, pointsType);
        }

        public void Clear()
        {
            Data = null;
            CurveList.Clear();
            GraphObjList.Clear();
        }

        public override void Draw(Graphics g)
        {
            GraphObjList.Clear();

            var data = Data;
            // Force Axes to recalculate to ensure proper layout of labels
            AxisChange(g);
            data.AddLabels(this, g);

            base.Draw(g);
        }

        public override void UpdateGraph(bool checkData)
        {
            GraphHelper.FormatGraphPane(this);
            var document = GraphSummary.DocumentUIContainer.DocumentUI;
            PeptideDocNode nodeSelected = null;
            var resultIndex = ShowReplicate == ReplicateDisplay.single ? GraphSummary.ResultsIndex : -1;
            var results = document.Settings.MeasuredResults;
            var resultsAvailable = results != null;
            if (resultsAvailable) 
            {
                if (resultIndex == -1)
                    resultsAvailable = results.IsLoaded;
                else
                    resultsAvailable = results.Chromatograms.Count > resultIndex &&
                                       results.IsChromatogramSetLoaded(resultIndex);
            }

            if (!resultsAvailable) 
            {
                Clear();
            }
            else 
            {
                var nodeTree = GraphSummary.StateProvider.SelectedNode as SrmTreeNode;
                var nodePeptide = nodeTree as PeptideTreeNode;
                while (nodePeptide == null && nodeTree != null) 
                {
                    nodeTree = nodeTree.Parent as SrmTreeNode;
                    nodePeptide = nodeTree as PeptideTreeNode;
                }
                if (nodePeptide != null)
                    nodeSelected = nodePeptide.DocNode;

                if (checkData)
                {                
                    PointsTypeMassError pointsType = MassErrorGraphController.PointsType;
                    Update(document, resultIndex, pointsType);
                }

                Graph(nodeSelected);
            }
        }

        public void Graph(PeptideDocNode nodeSelected)
        {
            var data = Data;
            if (data != null)
                data.Graph(this, nodeSelected);
        }

        /// <summary>
        /// Holds the data currently displayed in the graph.
        /// </summary>
        sealed class GraphData : Immutable
        {
            private readonly PpmBinCount[] _bins;
            private readonly double _mean;
            private readonly double _stdDev;
            private readonly double BIN_SIZE = 0.5;

            public GraphData(SrmDocument document, GraphData dataPrevious, int resultIndex, bool bestResult, PointsTypeMassError pointsType)
            {
                var vals = new List<double>();
                var dictPpmBin2ToCount = new Dictionary<int, int>();

                foreach (var nodePep in document.Molecules)
                {
                    if (MassErrorGraphController.PointsType == PointsTypeMassError.decoys) 
                    {
                        if (!nodePep.IsDecoy) continue;
                    }
                    else 
                    {
                        if (nodePep.IsDecoy) continue;
                    }

                    var tranIndex = bestResult ? nodePep.BestResult : resultIndex;
                    foreach (var nodeGroup in nodePep.TransitionGroups) 
                    {
                        if (tranIndex >= 0)
                        {
                            var chromInfo = nodeGroup.Results[tranIndex];
                            AddChromInfo(chromInfo, dictPpmBin2ToCount, vals, BIN_SIZE);
                        }
                        else
                        {
                            foreach (var chromInfo in nodeGroup.Results)
                                AddChromInfo(chromInfo, dictPpmBin2ToCount, vals, BIN_SIZE);
                        }
                    }
                }

                _bins = new PpmBinCount[dictPpmBin2ToCount.Count];
                var i = 0;
                foreach (var PpmBin in dictPpmBin2ToCount)
                {
                    _bins[i] = new PpmBinCount((float) (PpmBin.Key / (1/BIN_SIZE)), PpmBin.Value);
                    i++;
                }


                var statVals = new Statistics(vals.ToArray());
                _mean = statVals.Mean();
                _stdDev = statVals.StdDev();
            }

            private static void AddChromInfo(ChromInfoList<TransitionGroupChromInfo> chromInfos, Dictionary<int, int> dictPpmBin2ToCount, List<double> vals, double binSize)
            {
                if (chromInfos == null) return;
                foreach (var chromInfo in chromInfos) 
                {
                    var massError = chromInfo.MassError;
                    if (massError.HasValue) 
                    {
                        vals.Add((float)massError);
                        var ppmBin2 = (int)Math.Floor(massError.Value * (1/binSize));
                        if (dictPpmBin2ToCount.ContainsKey(ppmBin2))
                            dictPpmBin2ToCount[ppmBin2]++;
                        else
                            dictPpmBin2ToCount.Add(ppmBin2, 1);
                    }
                }
            }

            public void Graph(GraphPane graphPane, PeptideDocNode nodeSelected)
            {
                graphPane.CurveList.Clear();
                graphPane.BarSettings.ClusterScaleWidth = BIN_SIZE;
                graphPane.BarSettings.MinClusterGap = 0;

                var ps = new PointPairList();
                foreach (var bin in _bins) 
                    ps.Add(bin.Bin + (BIN_SIZE/2), bin.Count);

                graphPane.CurveList.Add(new BarItem(null, ps, Color.White));

                graphPane.XAxis.Title.Text = Resources.MassErrorReplicateGraphPane_UpdateGraph_Mass_Error;
                graphPane.YAxis.Title.Text = Resources.MassErrorHistogramGraphPane_UpdateGraph_Count;

                graphPane.XAxis.Scale.MaxAuto = graphPane.XAxis.Scale.MinAuto = true;
                graphPane.YAxis.Scale.MaxAuto = true;
                graphPane.YAxis.Scale.Min = 0;

                if (Settings.Default.MinMassError != 0) graphPane.XAxis.Scale.Min = Settings.Default.MinMassError;
                if (Settings.Default.MaxMassError != 0) graphPane.XAxis.Scale.Max = Settings.Default.MaxMassError;
                graphPane.AxisChange();

            }

            public void AddLabels(GraphPane graphPane, Graphics g)
            {
                if (_bins.Length == 0)
                {
                    graphPane.Title.Text = Resources.MassErrorHistogramGraphPane_AddLabels_Mass_Errors_Unavailable;
                    return;
                }
                var rectChart = graphPane.Chart.Rect;
                var ptTop = rectChart.Location;

                // Setup axes scales to enable the ReverseTransform method
                var xAxis = graphPane.XAxis;
                xAxis.Scale.SetupScaleData(graphPane, xAxis);
                var yAxis = graphPane.YAxis;
                yAxis.Scale.SetupScaleData(graphPane, yAxis);

                var yNext = ptTop.Y;
                var scoreLeft = xAxis.Scale.ReverseTransform(ptTop.X + 8);
                var timeTop = yAxis.Scale.ReverseTransform(yNext);

                var label = string.Format("{0} = {1:F01},\n" + "{2} = {3:F01}\n", // Not L10N
                Resources.MassErrorHistogramGraphPane_AddLabels_mean, _mean,
                Resources.MassErrorHistogramGraphPane_AddLabels_standard_deviation, _stdDev);

                var text = new TextObj(label, scoreLeft, timeTop, CoordType.AxisXYScale, AlignH.Left, AlignV.Top)
                {
                    IsClippedToChartRect = true,
                    ZOrder = ZOrder.E_BehindCurves,
                    FontSpec = GraphSummary.CreateFontSpec(Color.Black)
                };
                graphPane.GraphObjList.Add(text);
            }
        }

        private struct PpmBinCount
        {
            public PpmBinCount(float bin, int count)
                : this()
            {
                Bin = bin;
                Count = count;
            }

            public float Bin { get; private set; }
            public int Count { get; private set; }
        }
    }
}
