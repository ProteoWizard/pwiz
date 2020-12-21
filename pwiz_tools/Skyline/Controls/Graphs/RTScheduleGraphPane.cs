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
using pwiz.Common.SystemUtil;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using ZedGraph;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Controls.Graphs
{
    internal class RTScheduleGraphPane : SummaryGraphPane
    {
        private static readonly IList<Color> COLORS_WINDOW = GraphChromatogram.COLORS_LIBRARY;

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
        private SrmDocument _documentShowing;
        private double[] _windowsShowing;
        private string _brukerTemplate;
        private SchedulingMetrics _brukerMetricType;

        public RTScheduleGraphPane(GraphSummary graphSummary, bool isExportMethodDlg = false)
            : base(graphSummary)
        {
            _exportMethodDlg = isExportMethodDlg;

            XAxis.Title.Text = Resources.RTScheduleGraphPane_RTScheduleGraphPane_Scheduled_Time;
            YAxis.Scale.MinAuto = false;
            YAxis.Scale.Min = 0;
        }

        public override void UpdateGraph(bool selectionChanged)
        {
            SrmDocument document = !_exportMethodDlg ? GraphSummary.DocumentUIContainer.DocumentUI : Program.MainWindow.DocumentUI;
            var windows = ScheduleWindows;
            var brukerTemplate = BrukerTemplateFile;
            var brukerMetricType = BrukerMetricType;
            // No need to re-graph for a selection change
            if (ReferenceEquals(document, _documentShowing) && ArrayUtil.EqualsDeep(windows, _windowsShowing) &&
                Equals(BrukerTemplateFile, _brukerTemplate) && Equals(BrukerMetricType, _brukerMetricType))
                return;

            _documentShowing = document;
            _windowsShowing = windows;
            _brukerTemplate = brukerTemplate;
            _brukerMetricType = brukerMetricType;

            // TODO: Make it possible to see transition scheduling when full-scan enabled.
            if (string.IsNullOrEmpty(brukerTemplate))
            {
                XAxis.Title.Text = Resources.RTScheduleGraphPane_RTScheduleGraphPane_Scheduled_Time;
                YAxis.Title.Text = document.Settings.TransitionSettings.FullScan.IsEnabledMsMs
                    ? Resources.RTScheduleGraphPane_UpdateGraph_Concurrent_Precursors
                    : Resources.RTScheduleGraphPane_UpdateGraph_Concurrent_Transitions;
            }
            else if (BrukerMetrics == null)
            {
                XAxis.Title.Text = Resources.RTScheduleGraphPane_RTScheduleGraphPane_Scheduled_Time;
                YAxis.Title.Text = Resources.RTScheduleGraphPane_UpdateGraph_Concurrent_Accumulations;
            }
            else
            {
                switch (brukerMetricType)
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

            using (var longWait = new LongWaitDlg())
            {
                longWait.PerformWork(null, 800, progressMonitor =>
                {
                    AddCurve(document, Color.Blue, progressMonitor);
                    for (int i = 0; i < windows.Length; i++)
                    {
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

                        AddCurve(docWindow, COLORS_WINDOW[(i + 1) % COLORS_WINDOW.Count], progressMonitor);
                    }
                });
            }

            AxisChange();
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

        private void AddCurve(SrmDocument document, Color color, IProgressMonitor progressMonitor)
        {
            if (!string.IsNullOrEmpty(BrukerTemplateFile))
            {
                IPointList brukerPoints = null;
                if (BrukerMetrics != null)
                {
                    brukerPoints = BrukerMetrics.Get(BrukerMetricType);
                }
                else
                {
                    BrukerTimsTofMethodExporter.GetScheduling(document,
                        new ExportDlgProperties(new ExportMethodDlg(document, ExportFileType.Method), new CancellationToken()) {MethodType = ExportMethodType.Scheduled},
                        BrukerTemplateFile, progressMonitor, out brukerPoints);
                }
                AddCurve(document, brukerPoints, color);
                return;
            }

            var predict = document.Settings.PeptideSettings.Prediction;
            bool fullScan = document.Settings.TransitionSettings.FullScan.IsEnabledMsMs;

            // TODO: Guess this value from the document
            const bool singleWindow = false;

            List<PrecursorScheduleBase> listSchedules = new List<PrecursorScheduleBase>();
            double xMax = double.MinValue, xMin = double.MaxValue;

            foreach (var nodePep in document.Molecules)
            {
                foreach (TransitionGroupDocNode nodeGroup in nodePep.Children)
                {
                    double timeWindow;
                    double? retentionTime = predict.PredictRetentionTime(document, nodePep, nodeGroup, SchedulingReplicateIndex,                        
                        SchedulingAlgorithm, singleWindow, out timeWindow);
                    var nodeGroupPrimary = PrimaryTransitionCount > 0
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
                                                                 SchedulingReplicateIndex,
                                                                 PrimaryTransitionCount,
                                                                 0);
                        xMin = Math.Min(xMin, schedule.StartTime);
                        xMax = Math.Max(xMax, schedule.EndTime);
                        listSchedules.Add(schedule);
                    }
                }
            }

            PointPairList points = new PointPairList();
            xMin -= 1.0;
            xMax += 1.0;
            for (double x = xMin; x < xMax; x += 0.1)
                points.Add(x, PrecursorScheduleBase.GetOverlapCount(listSchedules, x));

            AddCurve(document, points, color);
        }

        private void AddCurve(SrmDocument document, IPointList points, Color color)
        {
            string label = string.Format(Resources.RTScheduleGraphPane_AddCurve__0__Minute_Window, GetSchedulingWindow(document));
            var curve = AddCurve(label, points, color);
            curve.Line.IsAntiAlias = true;
            curve.Line.IsOptimizedDraw = true;
            // TODO: Give this graph its own line width
            curve.Line.Width = Settings.Default.ChromatogramLineWidth;
            curve.Symbol.IsVisible = false;
        }
    }
}
