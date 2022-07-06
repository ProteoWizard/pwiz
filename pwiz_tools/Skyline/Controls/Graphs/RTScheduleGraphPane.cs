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
using System.Globalization;
using System.Linq;
using System.Threading;
using pwiz.CLI.Bruker.PrmScheduling;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Properties;
using ZedGraph;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Controls.Graphs
{
    internal class RTScheduleGraphPane : SummaryGraphPane
    {
        private static readonly IList<Color> COLORS_WINDOW = GraphChromatogram.COLORS_LIBRARY;
        private SchedulingDataCalculator _dataCalculator;

        public static double[] ScheduleWindows
        {
            get
            {
                string[] values = Settings.Default.RTScheduleWindows.Split(TextUtil.SEPARATOR_CSV);
                List<double> windows = new List<double>(values.Length);
                foreach (string value in values)
                {
                    double window;
                    if (double.TryParse(value.Trim(), NumberStyles.Float|NumberStyles.AllowThousands, NumberFormatInfo.InvariantInfo, out window))
                        windows.Add(window);
                }
                return windows.ToArray();
            }

            set
            {
                Settings.Default.RTScheduleWindows = string.Join(@",",
                    value.Select(v => v.ToString(CultureInfo.InvariantCulture)).ToArray());
            }
        }

        public static int PrimaryTransitionCount
        {
            get { return Settings.Default.PrimaryTransitionCountGraph; }
            set { Settings.Default.PrimaryTransitionCountGraph = value; }
        }

        public static string BrukerTemplateFile
        {
            get { return Settings.Default.BrukerPrmSqliteFile; }
            set { Settings.Default.BrukerPrmSqliteFile = value; }
        }

        public SchedulingMetrics BrukerMetricType { get; set; }
        public BrukerTimsTofMethodExporter.Metrics BrukerMetrics { get; set; }

        private bool _exportMethodDlg;
        private ZedGraphControl _graphControl;

        public RTScheduleGraphPane(GraphSummary graphSummary, ZedGraphControl graphControl, bool isExportMethodDlg = false)
            : base(graphSummary)
        {
            _exportMethodDlg = isExportMethodDlg;
            _graphControl = graphControl;
            _dataCalculator = new SchedulingDataCalculator(this, graphControl);

            XAxis.Title.Text = Resources.RTScheduleGraphPane_RTScheduleGraphPane_Scheduled_Time;
            YAxis.Scale.MinAuto = false;
            YAxis.Scale.Min = 0;
        }

        public override void UpdateGraph(bool selectionChanged)
        {
            var inputData = InputData.MakeInputData(this);
            var results = _dataCalculator.Results;
            if (results == null || !Equals(_dataCalculator.Input, inputData ))
            {
                _dataCalculator.Input = inputData;
                return;
            }

            var brukerTemplate = inputData.BrukerTemplateFileValue;
            var document = inputData.Document;
            // TODO: Make it possible to see transition scheduling when full-scan enabled.
            if (string.IsNullOrEmpty(brukerTemplate))
            {
                XAxis.Title.Text = Resources.RTScheduleGraphPane_RTScheduleGraphPane_Scheduled_Time;
                YAxis.Title.Text = document.Settings.TransitionSettings.FullScan.IsEnabledMsMs
                    ? Resources.RTScheduleGraphPane_UpdateGraph_Concurrent_Precursors
                    : Resources.RTScheduleGraphPane_UpdateGraph_Concurrent_Transitions;
            }
            else if (inputData.BrukerMetrics == null)
            {
                XAxis.Title.Text = Resources.RTScheduleGraphPane_RTScheduleGraphPane_Scheduled_Time;
                YAxis.Title.Text = Resources.RTScheduleGraphPane_UpdateGraph_Concurrent_Accumulations;
            }
            else
            {
                switch (inputData.BrukerMetricType)
                {
                    case SchedulingMetrics.CONCURRENT_FRAMES:
                        XAxis.Title.Text = Resources.RTScheduleGraphPane_RTScheduleGraphPane_Scheduled_Time;
                        YAxis.Title.Text = Resources.RTScheduleGraphPane_UpdateGraph_Concurrent_frames;
                        break;
                    case SchedulingMetrics.MAX_SAMPLING_TIMES:
                        XAxis.Title.Text = Resources.RTScheduleGraphPane_UpdateGraph_Target;
                        YAxis.Title.Text = Resources.RTScheduleGraphPane_UpdateGraph_Max_sampling_times;
                        break;
                    case SchedulingMetrics.MEAN_SAMPLING_TIMES:
                        XAxis.Title.Text = Resources.RTScheduleGraphPane_UpdateGraph_Target;
                        YAxis.Title.Text = Resources.RTScheduleGraphPane_UpdateGraph_Mean_sampling_times;
                        break;
                    case SchedulingMetrics.REDUNDANCY_OF_TARGETS:
                        XAxis.Title.Text = Resources.RTScheduleGraphPane_RTScheduleGraphPane_Scheduled_Time;
                        YAxis.Title.Text = Resources.RTScheduleGraphPane_UpdateGraph_Redundancy_of_targets;
                        break;
                    case SchedulingMetrics.TARGETS_PER_FRAME:
                        XAxis.Title.Text = Resources.RTScheduleGraphPane_RTScheduleGraphPane_Scheduled_Time;
                        YAxis.Title.Text = Resources.RTScheduleGraphPane_UpdateGraph_Targets_per_frame;
                        break;
                }
            }

            CurveList.Clear();
            foreach (var curveTuple in results.Curves)
            {
                AddSchedulingCurve(curveTuple.Item1, curveTuple.Item2, curveTuple.Item3);
            }

            AxisChange();
            _graphControl.Invalidate();
        }

        public void AddSchedulingCurve(string label, IPointList points, Color color)
        {
            var curve = AddCurve(label, points, color);
            curve.Line.IsAntiAlias = true;
            curve.Line.IsOptimizedDraw = true;
            // TODO: Give this graph its own line width
            curve.Line.Width = Settings.Default.ChromatogramLineWidth;
            curve.Symbol.IsVisible = false;
        }

        private static double? GetSchedulingWindow(SrmDocument document)
        {
            var predict = document.Settings.PeptideSettings.Prediction;
            if (document.Settings.HasResults && predict.UseMeasuredRTs)
                return predict.MeasuredRTWindow;
            if (predict.RetentionTime != null)
                return predict.RetentionTime.TimeWindow;
            return null;
        }

        public int? SchedulingReplicateIndex { get; set; }

        public ExportSchedulingAlgorithm SchedulingAlgorithm { get; set; }

        public class InputData
        {
            public static InputData MakeInputData(RTScheduleGraphPane graphPane)
            {
                return new InputData
                {
                    Document = !graphPane._exportMethodDlg ? graphPane.GraphSummary.DocumentUIContainer.DocumentUI : Program.MainWindow.DocumentUI,
                    BrukerMetricType = graphPane.BrukerMetricType,
                    BrukerMetrics = graphPane.BrukerMetrics,
                    BrukerTemplateFileValue = BrukerTemplateFile,
                    PrimaryTransitionCountValue = PrimaryTransitionCount,
                    SchedulingAlgorithm = graphPane.SchedulingAlgorithm,
                    SchedulingReplicateIndex = graphPane.SchedulingReplicateIndex,
                    SchedulingWindows = ImmutableList.ValueOf(RTScheduleGraphPane.ScheduleWindows)
                };
            }

            public SrmDocument Document { get; private set; }
            public ImmutableList<double> SchedulingWindows { get; private set; }
            public int PrimaryTransitionCountValue { get; private set; }
            public string BrukerTemplateFileValue { get; private set; }

            public SchedulingMetrics BrukerMetricType { get; private set; }
            public BrukerTimsTofMethodExporter.Metrics BrukerMetrics { get; private set; }
            public int? SchedulingReplicateIndex { get; set; }

            public ExportSchedulingAlgorithm SchedulingAlgorithm { get; set; }

            protected bool Equals(InputData other)
            {
                return Equals(Document, other.Document) && Equals(SchedulingWindows, other.SchedulingWindows) &&
                       PrimaryTransitionCountValue == other.PrimaryTransitionCountValue &&
                       BrukerTemplateFileValue == other.BrukerTemplateFileValue &&
                       BrukerMetricType == other.BrukerMetricType &&
                       Equals(BrukerMetrics, other.BrukerMetrics) &&
                       SchedulingReplicateIndex == other.SchedulingReplicateIndex &&
                       SchedulingAlgorithm == other.SchedulingAlgorithm;
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((InputData) obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = (Document != null ? Document.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ (SchedulingWindows != null ? SchedulingWindows.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ PrimaryTransitionCount;
                    hashCode = (hashCode * 397) ^ (BrukerTemplateFile != null ? BrukerTemplateFile.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ (int) BrukerMetricType;
                    hashCode = (hashCode * 397) ^ (BrukerMetrics != null ? BrukerMetrics.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ SchedulingReplicateIndex.GetHashCode();
                    hashCode = (hashCode * 397) ^ (int) SchedulingAlgorithm;
                    return hashCode;
                }
            }
        }

        public class Results
        {
            public Results(IEnumerable<Tuple<string, IPointList, Color>> curves)
            {
                Curves = ImmutableList.ValueOf(curves);
            }
            public ImmutableList<Tuple<string, IPointList, Color>> Curves { get; }
        }

        private class SchedulingDataCalculator : GraphDataCalculator<InputData, Results>
        {
            public SchedulingDataCalculator(RTScheduleGraphPane graphPane, ZedGraphControl graphControl) : base(CancellationToken.None, graphControl, graphPane)
            {
            }

            public new RTScheduleGraphPane GraphPane
            {
                get { return (RTScheduleGraphPane) base.GraphPane; }
            }

            protected override Results CalculateResults(InputData input, CancellationToken cancellationToken)
            {
                var document = input.Document;
                var curves = new List<Tuple<string, IPointList, Color>>();
                curves.Add(Tuple.Create(GetCurveTitle(document), MakeCurve(document, input, cancellationToken), Color.Blue));
                var windows = input.SchedulingWindows;
                for (int i = 0; i < windows.Count; i++)
                {
                    UpdateProgress(cancellationToken, i * 100 / windows.Count);
                    double window = windows[i];
                    // Do not show the window used by the current document twice.
                    if (window == GetSchedulingWindow(document))
                        continue;

                    var settings = document.Settings.ChangePeptidePrediction(p => p.ChangeMeasuredRTWindow(window));
                    if (settings.PeptideSettings.Prediction.RetentionTime != null)
                    {
                        settings = settings.ChangePeptidePrediction(p =>
                            p.ChangeRetentionTime(p.RetentionTime.ChangeTimeWindow(window)));
                    }
                    var docWindow = document.ChangeSettings(settings);

                    curves.Add(Tuple.Create(GetCurveTitle(docWindow), MakeCurve(docWindow, input, cancellationToken), COLORS_WINDOW[(i + 1) % COLORS_WINDOW.Count]));
                }
                return new Results(curves);
            }

            private IPointList MakeCurve(SrmDocument document, InputData input, CancellationToken cancellationToken)
            {
                if (!string.IsNullOrEmpty(input.BrukerTemplateFileValue))
                {
                    IPointList brukerPoints = null;
                    if (input.BrukerMetrics != null)
                    {
                        brukerPoints = input.BrukerMetrics.Get(input.BrukerMetricType);
                    }
                    else
                    {
                        try
                        {
                            BrukerTimsTofMethodExporter.GetScheduling(document,
                                new ExportDlgProperties(new ExportMethodDlg(document, ExportFileType.Method),
                                    new CancellationToken()) {MethodType = ExportMethodType.Scheduled},
                                input.BrukerTemplateFileValue, new SilentProgressMonitor(cancellationToken),
                                out brukerPoints);
                        }
                        catch (Exception)
                        {
                            // ignore "Scheduling failure (no points)" error
                        }
                    }

                    return brukerPoints;
                }

                var predict = document.Settings.PeptideSettings.Prediction;
                bool fullScan = document.Settings.TransitionSettings.FullScan.IsEnabledMsMs;

                // TODO: Guess this value from the document
                const bool singleWindow = false;

                List<PrecursorScheduleBase> listSchedules = new List<PrecursorScheduleBase>();
                double xMax = double.MinValue, xMin = double.MaxValue;
                int primaryTransitionCount = input.PrimaryTransitionCountValue;
                foreach (var nodePep in document.Molecules)
                {
                    foreach (TransitionGroupDocNode nodeGroup in nodePep.Children)
                    {
                        double timeWindow;
                        double? retentionTime = predict.PredictRetentionTime(document, nodePep, nodeGroup, input.SchedulingReplicateIndex,
                            input.SchedulingAlgorithm, singleWindow, out timeWindow);
                        var nodeGroupPrimary = primaryTransitionCount > 0
                            ? nodePep.GetPrimaryResultsGroup(nodeGroup)
                            : null;

                        if (retentionTime.HasValue)
                        {
                            // TODO: Make it possible to see transition scheduling when full-scan enabled.
                            var schedule = new PrecursorScheduleBase(nodeGroup,
                                nodeGroupPrimary,
                                retentionTime.Value,
                                timeWindow,
                                fullScan,
                                input.SchedulingReplicateIndex,
                                primaryTransitionCount,
                                0);
                            xMin = Math.Min(xMin, schedule.StartTime);
                            xMax = Math.Max(xMax, schedule.EndTime);
                            listSchedules.Add(schedule);
                        }
                    }
                }

                xMin -= 1.0;
                xMax += 1.0;
                PointPairList points = new PointPairList();
                foreach (var kvp in GetOverlapCounts(listSchedules, xMin, xMax, .01))
                {
                    points.Add(kvp.Key, kvp.Value);
                }

                return points;
            }

            public static IEnumerable<KeyValuePair<double, double>> GetOverlapCounts(
                IEnumerable<PrecursorScheduleBase> schedules, double minTime, double maxTime, double stepSize)
            {
                if (maxTime < minTime || double.IsNaN(maxTime) || double.IsNaN(minTime))
                {
                    return Enumerable.Empty<KeyValuePair<double, double>>();
                }
                int stepCount = (int)Math.Floor((maxTime - minTime) / stepSize) + 1;
                int[] overlapCounts = new int[stepCount];
                foreach (var schedule in schedules)
                {
                    int firstStep = (int)Math.Ceiling((schedule.StartTime - minTime) / stepSize);
                    int lastStep = (int)Math.Floor((schedule.EndTime - minTime) / stepSize);
                    firstStep = Math.Max(0, firstStep);
                    lastStep = Math.Min(lastStep, stepCount - 1);
                    for (int i = firstStep; i <= lastStep; i++)
                    {
                        overlapCounts[i] += schedule.TransitionCount;
                    }
                }

                return Enumerable.Range(0, stepCount).Select(step =>
                    new KeyValuePair<double, double>(minTime + stepSize * step, overlapCounts[step]));
            }


            protected override void ResultsAvailable()
            {
                GraphPane.UpdateGraph(false);
            }
        }

        public static string GetCurveTitle(SrmDocument document)
        {
            return string.Format(Resources.RTScheduleGraphPane_AddCurve__0__Minute_Window, GetSchedulingWindow(document));
        }
    }
}
