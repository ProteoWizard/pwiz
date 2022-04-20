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
using pwiz.MSGraph;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Controls.Graphs
{
    public class MassErrorHistogram2DGraphPane : SummaryGraphPane
    {
        public static ReplicateDisplay ShowReplicate
        {
            get { return Helpers.ParseEnum(Settings.Default.ShowRegressionReplicateEnum, ReplicateDisplay.all); }
        }

        private GraphData Data { get; set; }

        public MassErrorHistogram2DGraphPane(GraphSummary graphSummary)
            : base(graphSummary)
        {
        }

        public void Update(SrmDocument document, int resultIndex, PointsTypeMassError pointsType)
        {
            var bestResults = ShowReplicate == ReplicateDisplay.best;
            if (Data == null || !Data.IsCurrent(document, resultIndex, bestResults, pointsType))
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

            // Force Axes to recalculate to ensure proper layout of labels
            AxisChange(g);

            base.Draw(g);
        }

        public override void UpdateGraph(bool selectionChanged)
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
                {
                    resultsAvailable = results.Chromatograms.Count > resultIndex &&
                                       results.IsChromatogramSetLoaded(resultIndex);
                }
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

                Update(document, resultIndex, MassErrorGraphController.PointsType);

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
            // Cache variables for this data. Data only valid for this state
            private readonly SrmDocument _document;    // Active document when data was created
            private readonly int _resultIndex;    // Index to active replicate or -1 for everything
            private readonly ReplicateDisplay _replicateDisplay; // Replicate display when data was created
            private readonly DisplayTypeMassError _displayType; // Display type when data was created

            public readonly HeatMapData _heatMapData;
            private readonly int _maxCount;
            private double _maxMass = double.MinValue, _minMass = double.MaxValue, _maxX = double.MinValue, _minX = double.MaxValue;
            private readonly double _binSizePpm;
            private readonly TransitionMassError _transition;
            private readonly PointsTypeMassError _pointsType;
            private readonly Histogram2DXAxis _xAxis;
            private readonly int xAxisBins = 100;

            private const int MIN_DOT_RADIUS = 2;
            private const int MAX_DOT_RADIUS = 17;

            private static readonly int[,] EMPTY_COUNTS = new int[1,1];

            public GraphData(SrmDocument document, GraphData dataPrevious, int resultIndex,
                bool bestResult, PointsTypeMassError pointsType)
            {
                _document = document;
                _resultIndex = resultIndex;
                _replicateDisplay = ShowReplicate;

                int[,] counts2D = EMPTY_COUNTS;
                _displayType = MassErrorGraphController.HistogramDisplayType;
                _binSizePpm = Settings.Default.MassErorrHistogramBinSize;
                _transition = MassErrorGraphController.HistogramTransiton;
                _xAxis = MassErrorGraphController.Histogram2DXAxis;
                _pointsType = pointsType;
                if (_pointsType == PointsTypeMassError.targets_1FDR && !document.Settings.PeptideSettings.Integration.PeakScoringModel.IsTrained)
                    _pointsType = PointsTypeMassError.targets;

                bool decoys = pointsType == PointsTypeMassError.decoys;
                bool precursors = _displayType == DisplayTypeMassError.precursors;

                while (ReferenceEquals(counts2D, EMPTY_COUNTS))
                {
                    if (_maxMass != double.MinValue)
                       counts2D = new int[xAxisBins, (int)((_maxMass - _minMass) / _binSizePpm + 1)];

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
                                var mz = nodeTran.Mz.Value;
                                if (replicateIndex >= 0)
                                {
                                    AddChromInfo(nodeGroup, nodeTran, replicateIndex, mz, counts2D);
                                }
                                else 
                                {
                                    for (int i = 0; i < nodeTran.Results.Count; i++)
                                        AddChromInfo(nodeGroup, nodeTran, i, mz, counts2D);
                                }
                            }
                        }
                    }

                    // No values. Leave _maxCount == 0
                    if (_maxMass == double.MinValue)
                        return;
                }

                var points = new List<Point3D>();
                for (int x = 0; x < counts2D.GetLength(0); x++)
                {
                    for (int y = 0; y < counts2D.GetLength(1); y++)
                    {
                        int count = counts2D[x, y];
                        if (count > 0)
                        {
                            double binSizeX = (_maxX - _minX)/xAxisBins;
                            double xPoint = x*binSizeX + _minX + binSizeX/2;
                            double yPoint = y*_binSizePpm + _minMass + _binSizePpm/2;
                            points.Add(new Point3D(xPoint, yPoint, count));
                        }
                        _maxCount = Math.Max(_maxCount, count);
                    }
                }
                _heatMapData = new HeatMapData(points);
            }

            private void AddChromInfo(TransitionGroupDocNode nodeGroup, TransitionDocNode nodeTran, int replicateIndex,
                double mz, int[,] counts2D)
            {
                var chromGroupInfos = nodeGroup.Results[replicateIndex];
                var chromInfos = nodeTran.Results[replicateIndex];
                AddChromInfo(chromGroupInfos, chromInfos, mz, counts2D);
            }

            private void AddChromInfo(ChromInfoList<TransitionGroupChromInfo> chromGroupInfos, ChromInfoList<TransitionChromInfo> chromInfos,
                double mz, int[,] counts2D)
            {
                if (chromInfos.IsEmpty)
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
                        var xVal = _xAxis == Histogram2DXAxis.mass_to_charge ? mz : chromInfo.RetentionTime;
                        if (ReferenceEquals(counts2D, EMPTY_COUNTS))
                        {
                            _maxMass = Math.Max(_maxMass, massError.Value);
                            _minMass = Math.Min(_minMass, massError.Value);
                            _maxX = Math.Max(_maxX, xVal);
                            _minX = Math.Min(_minX, xVal);
                        }
                        else
                        {
                            int x = (int) Math.Floor((xVal - _minX)/((_maxX - _minX)/xAxisBins));
                            int y = (int) Math.Floor((massError.Value - _minMass)/_binSizePpm);
                            counts2D[Math.Min(x, counts2D.GetLength(0)-1), Math.Min(y, counts2D.GetLength(1)-1)]++;
                        }
                    }
                }
            }

            public bool IsCurrent(SrmDocument document, int resultIndex, bool bestResults, PointsTypeMassError pointsType)
            {
                return document != null && _document != null &&
                       ReferenceEquals(document.Children, _document.Children) &&
                       (bestResults || resultIndex == _resultIndex) &&
                       pointsType == _pointsType &&
                       Settings.Default.MassErorrHistogramBinSize == _binSizePpm &&
                       MassErrorGraphController.Histogram2DXAxis == _xAxis &&
                       MassErrorGraphController.HistogramDisplayType == _displayType &&
                       MassErrorGraphController.HistogramTransiton == _transition &&
                       ShowReplicate == _replicateDisplay;
            }

            public void Graph(MassErrorHistogram2DGraphPane graphPane, PeptideDocNode nodeSelected)
            {
                graphPane.Title.Text = string.Empty;
                graphPane.YAxis.Title.Text = Resources.MassErrorReplicateGraphPane_UpdateGraph_Mass_Error;
                graphPane.XAxis.Title.Text = _xAxis == Histogram2DXAxis.mass_to_charge
                    ? Resources.MassErrorHistogram2DGraphPane_Graph_Mz
                    : Resources.MassErrorHistogram2DGraphPane_Graph_Retention_Time;
                if (_maxCount == 0) {
                    graphPane.Title.Text = Resources.MassErrorHistogramGraphPane_AddLabels_Mass_Errors_Unavailable;
                    graphPane.CurveList.Clear();
                    return;
                }
                graphPane.YAxis.Scale.Min = _minMass;
                graphPane.YAxis.Scale.Max = _maxMass;
                graphPane.XAxis.Scale.Min = _minX;
                graphPane.XAxis.Scale.Max = _maxX;
                graphPane.AxisChange();
                HeatMapGraphPane.GraphHeatMap(graphPane,
                    _heatMapData, MAX_DOT_RADIUS, MIN_DOT_RADIUS, (float)_minMass, (float)_maxMass,
                    Settings.Default.MassErrorHistogram2DLogScale, 5);
            }
        }

        public int GetPoints()
        {
           var points =  Data._heatMapData.GetPoints(-1000, 1000, -1000, 1000, 0.001,0.001);
            return points.Count;
        }
    }
}
