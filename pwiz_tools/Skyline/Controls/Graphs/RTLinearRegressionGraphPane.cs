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
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using ZedGraph;

namespace pwiz.Skyline.Controls.Graphs
{
    internal sealed class RTLinearRegressionGraphPane : SummaryGraphPane
    {
        public static readonly Color COLOR_REFINED = Color.DarkBlue;
        public static readonly Color COLOR_LINE_REFINED = Color.Black;
        public static readonly Color COLOR_LINE_PREDICT = Color.DarkGray;
        public static readonly Color COLOR_OUTLIERS = Color.BlueViolet;
        public static readonly Color COLOR_LINE_ALL = Color.BlueViolet;

        private GraphData _data;

        public RTLinearRegressionGraphPane(GraphSummary graphSummary)
            : base(graphSummary)
        {
            XAxis.Title.Text = "Score";
            YAxis.Title.Text = "Measured Time";
            YAxis.Scale.MinAuto = false;
            YAxis.Scale.Min = 0;
        }

        public override bool HandleMouseMoveEvent(ZedGraphControl sender, MouseEventArgs e)
        {
            if (PeptideIndexFromPoint(new PointF(e.X, e.Y)) != null)
            {
                GraphSummary.Cursor = Cursors.Hand;
                return true;
            }
            return base.HandleMouseMoveEvent(sender, e);
        }

        public override bool HandleMouseDownEvent(ZedGraphControl sender, MouseEventArgs e)
        {
            var peptideIndex = PeptideIndexFromPoint(new PointF(e.X, e.Y));
            if (peptideIndex != null)
            {
                var pathSelect = GraphSummary.DocumentUIContainer.DocumentUI.GetPathTo((int)SrmDocument.Level.Peptides,
                                                                                             peptideIndex.IndexDoc);
                GraphSummary.StateProvider.SelectedPath = pathSelect;
                return true;
            }
            return false;
        }

        public bool AllowDeletePoint(PointF point)
        {
            return PeptideIndexFromPoint(point) != null;
        }

        private GraphData Data
        {
            get
            {
                return Interlocked.Exchange(ref _data, _data);
            }
            set
            {
                Interlocked.Exchange(ref _data, value);
            }
        }

        public bool HasOutliers
        {
            get
            {
                var data = Data;
                return data != null ? data.HasOutliers : false;
            }
        }

        public PeptideDocNode[] Outliers
        {
            get
            { 
                GraphData data = Data;
                return data == null ? null : data.Outliers;
            }
        }

        public static PeptideDocNode[] CalcOutliers(SrmDocument document, double threshold)
        {
            var data = new GraphData(document, null, -1, threshold, true);
            return data.Refine(() => false).Outliers;
        }

        public RetentionTimeRegression RegressionRefined
        {
            get
            { 
                GraphData data = Data;
                return data == null ? null : data.RegressionRefined;
            }
        }

        public bool IsValidFor(SrmDocument document)
        {
            var data = Data;
            return data != null && data.IsValidFor(document);
        }

        public bool IsValidFor(SrmDocument document, int resultIndex, double threshold, bool refine)
        {
            var data = Data;
            return data != null && data.IsValidFor(document, resultIndex, threshold, refine);
        }

        public void Clear()
        {
            Data = null;
            CurveList.Clear();
            GraphObjList.Clear();
        }

        public void Graph(PeptideDocNode nodeSelected)
        {
            var data = Data;
            if (data != null)
                data.Graph(this, nodeSelected);
        }

        public void Update(SrmDocument document, int resultIndex, double threshold, bool refine)
        {
            Data = new GraphData(document, Data, resultIndex, threshold, refine);
        }

        public bool IsRefined
        {
            get
            {
                var data = Data;
                return data != null && data.IsRefined();
            }
        }

        public bool Refine(Func<bool> isCanceled)
        {
            GraphData dataCurrent = Data;
            GraphData dataNew = dataCurrent.Refine(isCanceled);

            // No refinement happened, if data did not change
            if (ReferenceEquals(dataNew, dataCurrent))
                return false;

            // Threadsafe update of the data
            GraphData dataPrevious = Interlocked.CompareExchange(ref _data, dataNew, dataCurrent);
            return ReferenceEquals(dataPrevious, dataCurrent);
        }

        public override void Draw(Graphics g)
        {
            GraphObjList.Clear();

            var data = Data;
            if (data != null)
            {
                // Force Axes to recalculate to ensure proper layout of labels
                AxisChange(g);
                data.AddLabels(this, g);
            }

            base.Draw(g);
        }

        public PeptideDocumentIndex PeptideIndexFromPoint(PointF point)
        {
            var data = Data;
            return data != null ? data.PeptideIndexFromPoint(this, point) : null;
        }

        private const int OVER_THRESHOLD = 4;

        public bool PointIsOver(PointF point, double score, double time)
        {
            float x = XAxis.Scale.Transform(score);
            if (Math.Abs(x - point.X) > OVER_THRESHOLD)
                return false;
            float y = YAxis.Scale.Transform(time);
            if (Math.Abs(y - point.Y) > OVER_THRESHOLD)
                return false;
            return true;
        }

        public override void UpdateGraph(bool checkData)
        {
            SrmDocument document = GraphSummary.DocumentUIContainer.DocumentUI;
            PeptideDocNode nodeSelected = null;
            int resultIndex = (Settings.Default.RTAverageReplicates ? -1 : GraphSummary.ResultsIndex);
            var results = document.Settings.MeasuredResults;
            bool resultsAvailable = results != null;
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
                    double threshold = RTGraphController.OutThreshold;
                    bool refine = Settings.Default.RTRefinePeptides;

                    if (!IsValidFor(document, resultIndex, threshold, refine))
                    {
                        Update(document, resultIndex, threshold, refine);
                        if (refine && !IsRefined)
                        {
                            // Do refinement on a background thread.
                            Action refineData = RefineData;
                            refineData.BeginInvoke(null, null);
                        }
                    }
                }

                Graph(nodeSelected);
            }

            AxisChange();
            GraphSummary.GraphControl.Invalidate();
        }

        /// <summary>
        /// For execution of refinement on a background thread, with cancelation
        /// if a the document changes.
        /// </summary>
        private void RefineData()
        {
            try
            {
                if (Refine(() => !IsValidFor(GraphSummary.DocumentUIContainer.Document)))
                {
                    // Update the graph on the UI thread.
                    Action<bool> update = UpdateGraph;
                    GraphSummary.BeginInvoke(update, false);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        /// <summary>
        /// Holds the data currently displayed in the graph.
        /// </summary>
        sealed class GraphData : Immutable
        {
            private readonly SrmDocument _document;
            private readonly int _resultIndex;
            private readonly double _threshold;
            private readonly bool _refine;
            private readonly List<PeptideDocumentIndex> _peptidesIndexes;
            private readonly List<MeasuredRetentionTime> _peptidesTimes;
            private readonly RetentionTimeScoreCache _scoreCache;

            private readonly RetentionTimeRegression _regressionPredict;
            private readonly RetentionTimeStatistics _statisticsPredict;

            private readonly RetentionTimeRegression _regressionAll;
            private readonly RetentionTimeStatistics _statisticsAll;

            private RetentionTimeRegression _regressionRefined;
            private RetentionTimeStatistics _statisticsRefined;

            private double[] _timesRefined;
            private double[] _scoresRefined;
            private double[] _timesOutliers;
            private double[] _scoresOutliers;

            private HashSet<int> _outlierIndexes;

            public GraphData(SrmDocument document, GraphData dataPrevious, int resultIndex, double threshold, bool refine)
            {
                _document = document;
                _resultIndex = resultIndex;
                _threshold = threshold;
                _peptidesIndexes = new List<PeptideDocumentIndex>();
                _peptidesTimes = new List<MeasuredRetentionTime>();
                int index = -1;
                foreach (var nodePeptide in document.Peptides)
                {
                    index++;
                    float? rt = nodePeptide.GetMeasuredRetentionTime(resultIndex);
                    if (!rt.HasValue)
                        rt = 0;

                    _peptidesIndexes.Add(new PeptideDocumentIndex(nodePeptide, index));
                    _peptidesTimes.Add(new MeasuredRetentionTime(nodePeptide.Peptide.Sequence, rt.Value));
                }

                _scoreCache = RetentionTimeRegression.CreateScoreCache(_peptidesTimes,
                                                                       dataPrevious != null ? dataPrevious._scoreCache : null);
                _regressionAll = RetentionTimeRegression.CalcRegression("graph", null,
                                                                        _peptidesTimes, _scoreCache, out _statisticsAll);
                if (_regressionAll != null)
                {
                    _scoresRefined = _statisticsAll.ListHydroScores.ToArray();
                    _timesRefined = _statisticsAll.ListRetentionTimes.ToArray();
                }

                _regressionPredict = document.Settings.PeptideSettings.Prediction.RetentionTime;
                if (_regressionPredict != null)
                {
                    IDictionary<string, double> scoreCache = null;
                    if (_regressionAll != null && ReferenceEquals(_regressionAll.Calculator, _regressionPredict.Calculator))
                        scoreCache = _statisticsAll.ScoreCache;
                    _statisticsPredict = _regressionPredict.CalcStatistics(_peptidesTimes, scoreCache);
                }

                // Only refine, if not already exceeding the threshold
                _refine = refine && !IsRefined();
            }

            public bool IsValidFor(SrmDocument document)
            {
                return ReferenceEquals(document, _document);
            }

            public bool IsValidFor(SrmDocument document, int resultIndex, double threshold, bool refine)
            {
                return IsValidFor(document) && _resultIndex == resultIndex && _threshold == threshold &&
                       // Valid if refine is true, and this data requires no further refining
                       (_refine == refine || (refine && IsRefined()));
            }

            public RetentionTimeRegression RegressionRefined
            {
                get { return _regressionRefined ?? _regressionAll; }
            }

            public bool IsRefined()
            {
                // If refinement has been performed, or it doesn't need to be.
                if (_regressionRefined != null)
                    return true;
                if (_statisticsAll == null)
                    return false;
                return RetentionTimeRegression.IsAboveThreshold(_statisticsAll.R, _threshold);
            }

            public GraphData Refine(Func<bool> isCanceled)
            {
                if (IsRefined())
                    return this;
                var result = ImClone(this).RefineCloned(_threshold, isCanceled);
                if (result == null)
                    return this;
                return result;
            }

            private GraphData RefineCloned(double threshold, Func<bool> isCanceled)
            {
                // Create list of deltas between predicted and measured times
                _outlierIndexes = new HashSet<int>();
                // Start with anything assigned a zero retention time as outliers
                for (int i = 0; i < _peptidesTimes.Count; i++)
                {
                    if (_peptidesTimes[i].RetentionTime == 0)
                        _outlierIndexes.Add(i);
                }

                _regressionRefined = (_regressionAll == null ? null :
                    _regressionAll.FindThreshold(threshold,
                                                 2,
                                                 _peptidesTimes.Count,
                                                 _peptidesTimes,
                                                 _statisticsAll,
                                                 _scoreCache,
                                                 isCanceled,
                                                 ref _statisticsRefined,
                                                 ref _outlierIndexes));

                if (ReferenceEquals(_regressionRefined, _regressionAll))
                    return null;

                // Separate lists into acceptable and outliers
                var listScoresRefined = new List<double>();
                var listTimesRefined = new List<double>();
                var listScoresOutliers = new List<double>();
                var listTimesOutliers = new List<double>();
                for (int i = 0; i < _scoresRefined.Length; i++)
                {
                    if (_outlierIndexes.Contains(i))
                    {
                        listScoresOutliers.Add(_scoresRefined[i]);
                        listTimesOutliers.Add(_timesRefined[i]);
                    }
                    else
                    {
                        listScoresRefined.Add(_scoresRefined[i]);
                        listTimesRefined.Add(_timesRefined[i]);
                    }
                }
                _scoresRefined = listScoresRefined.ToArray();
                _timesRefined = listTimesRefined.ToArray();
                _scoresOutliers = listScoresOutliers.ToArray();
                _timesOutliers = listTimesOutliers.ToArray();

                return this;
            }

            public PeptideDocumentIndex PeptideIndexFromPoint(RTLinearRegressionGraphPane graphPane, PointF point)
            {
                int iRefined = 0, iOut = 0;
                for (int i = 0; i < _peptidesIndexes.Count; i++)
                {
                    if (_outlierIndexes != null && _outlierIndexes.Contains(i))
                    {
                        if (graphPane.PointIsOver(point, _scoresOutliers[iOut], _timesOutliers[iOut]))
                            return _peptidesIndexes[i];
                        iOut++;
                    }
                    else if (_scoresRefined != null && _timesRefined != null)
                    {
                        if (graphPane.PointIsOver(point, _scoresRefined[iRefined], _timesRefined[iRefined]))
                            return _peptidesIndexes[i];
                        iRefined++;
                    }
                }
                return null;
            }

            private bool PointFromPeptide(PeptideDocNode nodePeptide, out double score, out double time)
            {
                if (nodePeptide != null && _regressionAll != null)
                {
                    int iRefined = 0, iOut = 0;
                    for (int i = 0; i < _peptidesIndexes.Count; i++)
                    {
                        if (_outlierIndexes != null && _outlierIndexes.Contains(i))
                        {
                            if (ReferenceEquals(nodePeptide, _peptidesIndexes[i].DocNode))
                            {
                                score = _scoresOutliers[iOut];
                                time = _timesOutliers[iOut];
                                return true;
                            }
                            iOut++;
                        }
                        else
                        {
                            if (ReferenceEquals(nodePeptide, _peptidesIndexes[i].DocNode))
                            {
                                score = _scoresRefined[iRefined];
                                time = _timesRefined[iRefined];
                                return true;
                            }
                            iRefined++;
                        }
                    }
                }
                score = 0;
                time = 0;
                return false;
            }

            public bool HasOutliers { get { return _outlierIndexes != null && _outlierIndexes.Count > 0; } }

            public PeptideDocNode[] Outliers
            {
                get
                {
                    if (!HasOutliers)
                        return new PeptideDocNode[0];

                    var listOutliers = new List<PeptideDocNode>();
                    for (int i = 0; i < _peptidesIndexes.Count; i++)
                    {
                        if (_outlierIndexes.Contains(i))
                            listOutliers.Add(_peptidesIndexes[i].DocNode);
                    }
                    return listOutliers.ToArray();
                }
            }

            public void Graph(GraphPane graphPane, PeptideDocNode nodeSelected)
            {
                graphPane.CurveList.Clear();

                double scoreSelected, timeSelected;
                if (PointFromPeptide(nodeSelected, out scoreSelected, out timeSelected))
                {
                    Color colorSelected = GraphSummary.ColorSelected;
                    var curveOut = graphPane.AddCurve(null, new[] { scoreSelected }, new[] { timeSelected },
                                                      colorSelected, SymbolType.Diamond);
                    curveOut.Line.IsVisible = false;
                    curveOut.Symbol.Fill = new Fill(colorSelected);
                    curveOut.Symbol.Size = 8f;
                }

                string labelPoints = "Peptides";
                if (!_refine)
                {
                    GraphRegression(graphPane, _statisticsAll, "Regression", COLOR_LINE_REFINED);
                }
                else
                {
                    labelPoints = "Peptides Refined";
                    GraphRegression(graphPane, _statisticsRefined, "Regression Refined", COLOR_LINE_REFINED);
                    GraphRegression(graphPane, _statisticsAll, "Regression", COLOR_LINE_ALL);
                }

                if (_regressionPredict != null && Settings.Default.RTPredictorVisible)
                {
                    GraphRegression(graphPane, _statisticsPredict, "Predictor", COLOR_LINE_PREDICT);
                }

                var curve = graphPane.AddCurve(labelPoints, _scoresRefined, _timesRefined,
                                               Color.Black, SymbolType.Diamond);
                curve.Line.IsVisible = false;
                curve.Symbol.Border.IsVisible = false;
                curve.Symbol.Fill = new Fill(COLOR_REFINED);

                if (_scoresOutliers != null)
                {
                    var curveOut = graphPane.AddCurve("Outliers", _scoresOutliers, _timesOutliers,
                                                      Color.Black, SymbolType.Diamond);
                    curveOut.Line.IsVisible = false;
                    curveOut.Symbol.Border.IsVisible = false;
                    curveOut.Symbol.Fill = new Fill(COLOR_OUTLIERS);
                }
            }

            private static void GraphRegression(GraphPane graphPane,
                                                RetentionTimeStatistics statistics, string name, Color color)
            {
                double[] lineScores, lineTimes;
                if (statistics == null)
                {
                    lineScores = new double[0];
                    lineTimes = new double[0];
                }
                else
                {
                    // Find maximum hydrophobicity score points for drawing the regression line
                    lineScores = new[] { double.MaxValue, 0 };
                    lineTimes = new[] { double.MaxValue, 0 };

                    for (int i = 0; i < statistics.ListHydroScores.Count; i++)
                    {
                        double score = statistics.ListHydroScores[i];
                        double time = statistics.ListPredictions[i];
                        if (score < lineScores[0])
                        {
                            lineScores[0] = score;
                            lineTimes[0] = time;
                        }
                        if (score > lineScores[1])
                        {
                            lineScores[1] = score;
                            lineTimes[1] = time;
                        }
                    }
                }

                var curve = graphPane.AddCurve(name, lineScores, lineTimes, color);
                curve.Line.IsAntiAlias = true;
                curve.Line.IsOptimizedDraw = true;
            }

            public void AddLabels(GraphPane graphPane, Graphics g)
            {
                RectangleF rectChart = graphPane.Chart.Rect;
                PointF ptTop = rectChart.Location;

                // Setup axes scales to enable the ReverseTransform method
                var xAxis = graphPane.XAxis;
                xAxis.Scale.SetupScaleData(graphPane, xAxis);
                var yAxis = graphPane.YAxis;
                yAxis.Scale.SetupScaleData(graphPane, yAxis);

                float yNext = ptTop.Y;
                double scoreLeft = xAxis.Scale.ReverseTransform(ptTop.X + 8);
                double timeTop = yAxis.Scale.ReverseTransform(yNext);
                if (!_refine)
                {
                    yNext += AddRegressionLabel(graphPane, g, scoreLeft, timeTop,
                                                _regressionAll, _statisticsAll, COLOR_LINE_REFINED);
                }
                else
                {
                    yNext += AddRegressionLabel(graphPane, g, scoreLeft, timeTop,
                                                _regressionRefined, _statisticsRefined, COLOR_LINE_REFINED);
                    timeTop = yAxis.Scale.ReverseTransform(yNext);
                    yNext += AddRegressionLabel(graphPane, g, scoreLeft, timeTop,
                                                _regressionAll, _statisticsAll, COLOR_LINE_ALL);
                }

                if (_regressionPredict != null && Settings.Default.RTPredictorVisible)
                {
                    timeTop = yAxis.Scale.ReverseTransform(yNext);
                    AddRegressionLabel(graphPane, g, scoreLeft, timeTop,
                                       _regressionPredict, _statisticsPredict, COLOR_LINE_PREDICT);
                }
            }

            private static float AddRegressionLabel(PaneBase graphPane, Graphics g, double score, double time,
                                                    RetentionTimeRegression regression, RetentionTimeStatistics statistics, Color color)
            {
                string label;
                if (regression == null || statistics == null)
                {
                    label = "slope = ?, intercept = ?\n" +
                            "window = ?\n" +
                            "r = ?";
                }
                else
                {
                    label = string.Format("slope = {0:F02}, intercept = {1:F02}\n" +
                                          "window = {2:F01}\n" +
                                          "r = {3:F02}",
                                          regression.Conversion.Slope,
                                          regression.Conversion.Intercept,
                                          regression.TimeWindow,
                                          statistics.R);
                }

                TextObj text = new TextObj(label, score, time,
                                           CoordType.AxisXYScale, AlignH.Left, AlignV.Top)
                                   {
                                       IsClippedToChartRect = true,
                                       ZOrder = ZOrder.E_BehindCurves,
                                       FontSpec = GraphSummary.CreateFontSpec(color),
                                   };
                graphPane.GraphObjList.Add(text);

                // Measure the text just added, and return its height
                SizeF sizeLabel = text.FontSpec.MeasureString(g, label, graphPane.CalcScaleFactor());
                return sizeLabel.Height + 3;
            }
        }
    }

    internal sealed class PeptideDocumentIndex
    {
        public PeptideDocumentIndex(PeptideDocNode docNode, int indexDoc)
        {
            DocNode = docNode;
            IndexDoc = indexDoc;
        }

        public PeptideDocNode DocNode { get; private set; }
        public int IndexDoc { get; private set; }
    }
}