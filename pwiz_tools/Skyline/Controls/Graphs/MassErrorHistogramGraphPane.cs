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
using System.Linq;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using ZedGraph;

namespace pwiz.Skyline.Controls.Graphs
{
    public class MassErrorHistogramGraphPane : SummaryGraphPane
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
            if (Data == null || !Data.IsCurrent(document, resultIndex, bestResults, pointsType))
                Data = new GraphData(document, resultIndex, bestResults, pointsType);
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

        public override void UpdateGraph(bool selectionChanged)
        {
            GraphHelper.FormatGraphPane(this);
            var document = GraphSummary.DocumentUIContainer.DocumentUI;
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
     
            Update(resultsAvailable ? document : null, resultIndex, MassErrorGraphController.PointsType);

            Graph();
        }

        public void Graph()
        {
            var data = Data;
            if (data != null)
                data.Graph(this);
        }

        public double Mean => Data.Mean;
        public double StdDev => Data.StdDev;

        /// <summary>
        /// Holds the data currently displayed in the graph.
        /// </summary>
        sealed class GraphData : Immutable
        {
            // Cache variables for this data. Data only valid for this state
            private readonly SrmDocument _document;	// Active document when data was created
            private readonly int _resultIndex;	// Index to active replicate or -1 for everything
            private readonly DisplayTypeMassError _displayType; // Display type when data was created
            private readonly ReplicateDisplay _replicateDisplay; // Replicate dsiaply when data was created

            private readonly PpmBinCount[] _bins;
            private readonly double _binSize;
            private readonly double _mean;
            private readonly double _stdDev;
            private readonly TransitionMassError _transition;
            private readonly PointsTypeMassError _pointsType;

            public GraphData(SrmDocument document, int resultIndex, bool bestResult, PointsTypeMassError pointsType)
            {
                _document = document;
                _resultIndex = resultIndex;
                _replicateDisplay = ShowReplicate;

                var vals = new List<double>();
                var dictPpmBin2ToCount = new Dictionary<int, int>();
                _displayType = MassErrorGraphController.HistogramDisplayType;
                _binSize = Settings.Default.MassErorrHistogramBinSize;
                _transition = MassErrorGraphController.HistogramTransiton;
                _pointsType = pointsType;

                if (document != null)
                {
                    bool decoys = pointsType == PointsTypeMassError.decoys;
                    bool precursors = _displayType == DisplayTypeMassError.precursors;

                    foreach (var nodePep in document.Molecules)
                    {
                        if (decoys != nodePep.IsDecoy)
                            continue;

                        var replicateIndex = bestResult && nodePep.BestResult != -1 ? nodePep.BestResult : resultIndex;
                        foreach (var nodeGroup in nodePep.TransitionGroups)
                        {
                            foreach (var nodeTran in nodeGroup.Transitions)
                            {
                                if (precursors != nodeTran.IsMs1)
                                    continue;
                                if (replicateIndex >= 0)
                                {
                                    AddChromInfo(nodeGroup, nodeTran, replicateIndex, dictPpmBin2ToCount, vals);
                                }
                                else
                                {
                                    for (int i = 0; i < nodeTran.Results.Count; i++)
                                        AddChromInfo(nodeGroup, nodeTran, i, dictPpmBin2ToCount, vals);
                                }
                            }
                        }
                    }
                }

                _bins = dictPpmBin2ToCount.Select(ppmBin => new PpmBinCount((float)(ppmBin.Key * _binSize), ppmBin.Value)).ToArray();

                var statVals = new Statistics(vals.ToArray());
                _mean = statVals.Mean();
                _stdDev = statVals.StdDev();
            }

            public double Mean => _mean;
            public double StdDev => _stdDev;

            private void AddChromInfo(TransitionGroupDocNode nodeGroup, TransitionDocNode nodeTran, int replicateIndex,
                Dictionary<int, int> dictPpmBin2ToCount, List<double> vals)
            {
                var chromGroupInfos = nodeGroup.Results[replicateIndex];
                var chromInfos = nodeTran.Results[replicateIndex];
                AddChromInfo(chromGroupInfos, chromInfos, dictPpmBin2ToCount, vals);
            }

            private void AddChromInfo(ChromInfoList<TransitionGroupChromInfo> chromGroupInfos, ChromInfoList<TransitionChromInfo> chromInfos,
                Dictionary<int, int> dictPpmBin2ToCount, List<double> vals)
            {
                if (chromInfos.IsEmpty || chromGroupInfos.IsEmpty)
                    return;
                foreach (var chromInfo in chromInfos) 
                {
                    if (_transition == TransitionMassError.best && chromInfo.RankByLevel != 1)
                        continue;
                    if (_pointsType == PointsTypeMassError.targets_1FDR && chromInfo.GetMatchingQValue(chromGroupInfos) > 0.01)
                        continue;
                    var massError = chromInfo.MassError;
                    if (massError.HasValue) 
                    {
                        vals.Add((float)massError);
                        var ppmBin2 = (int)Math.Floor(massError.Value / _binSize);
                        if (dictPpmBin2ToCount.ContainsKey(ppmBin2))
                            dictPpmBin2ToCount[ppmBin2]++;
                        else
                            dictPpmBin2ToCount.Add(ppmBin2, 1);
                    }
                }
            }

            public bool IsCurrent(SrmDocument document, int resultIndex, bool bestResults, PointsTypeMassError pointsType)
            {
                return document != null && _document != null &&
                       ReferenceEquals(document.Children, _document.Children) &&
                       (bestResults || resultIndex == _resultIndex) &&
                       pointsType == _pointsType &&
                       Settings.Default.MassErorrHistogramBinSize == _binSize &&
                       MassErrorGraphController.HistogramDisplayType == _displayType &&
                       MassErrorGraphController.HistogramTransiton == _transition &&
                       ShowReplicate == _replicateDisplay;
            }

            public void Graph(GraphPane graphPane)
            {
                graphPane.CurveList.Clear();
                graphPane.BarSettings.ClusterScaleWidth = _binSize;
                graphPane.BarSettings.MinClusterGap = 0;

                var ps = new PointPairList();
                foreach (var bin in _bins)
                    ps.Add(bin.Bin + _binSize/2, bin.Count);
                var bar = new BarItem(null, ps, Color.FromArgb(180, 220, 255));
                bar.Bar.Fill.Type = FillType.Solid;
                graphPane.CurveList.Add(bar);

                graphPane.XAxis.Title.Text = Resources.MassErrorReplicateGraphPane_UpdateGraph_Mass_Error;
                graphPane.YAxis.Title.Text = Resources.MassErrorHistogramGraphPane_UpdateGraph_Count;

                graphPane.XAxis.Scale.MaxAuto = graphPane.XAxis.Scale.MinAuto = true;
                graphPane.YAxis.Scale.MaxAuto = true;
                graphPane.YAxis.Scale.Min = 0;

                if (Settings.Default.MinMassError != 0)
                    graphPane.XAxis.Scale.Min = Settings.Default.MinMassError;
                if (Settings.Default.MaxMassError != 0)
                    graphPane.XAxis.Scale.Max = Settings.Default.MaxMassError;
                graphPane.AxisChange();

            }

            public void AddLabels(GraphPane graphPane, Graphics g)
            {
                if (_bins.Length == 0)
                {
                    graphPane.Title.Text = Resources.MassErrorHistogramGraphPane_AddLabels_Mass_Errors_Unavailable;
                    return;
                }
                graphPane.Title.Text = String.Empty;

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

                // ReSharper disable LocalizableElement
                var label = string.Format("{0} = {1:F01},\n" + "{2} = {3:F01}\n",
                // ReSharper restore LocalizableElement
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
