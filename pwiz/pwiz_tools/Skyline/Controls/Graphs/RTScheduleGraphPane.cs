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
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Properties;
using ZedGraph;

namespace pwiz.Skyline.Controls.Graphs
{
    internal class RTScheduleGraphPane : SummaryGraphPane
    {
        private static readonly Color[] COLORS_WINDOW = GraphChromatogram.COLORS_LIBRARY;

        public static double[] ScheduleWindows
        {
            get
            {
                string[] values = Settings.Default.RTScheduleWindows.Split(',');
                List<double> windows = new List<double>(values.Length);
                foreach (string value in values)
                {
                    double window;
                    if (double.TryParse(value, out window))
                        windows.Add(window);
                }
                return windows.ToArray();
            }
        }

        public RTScheduleGraphPane(GraphSummary graphSummary)
            : base(graphSummary)
        {
            XAxis.Title.Text = "Scheduled Time";
            YAxis.Title.Text = "Concurrent Transitions";
            YAxis.Scale.MinAuto = false;
            YAxis.Scale.Min = 0;
        }

        public override void UpdateGraph(bool checkData)
        {
            SrmDocument document = GraphSummary.DocumentUIContainer.DocumentUI;

            CurveList.Clear();

            AddCurve(document, Color.Blue);
            var windows = ScheduleWindows;
            for (int i = 0; i < windows.Length; i++)
            {
                double window = windows[i];
                if (window == document.Settings.PeptideSettings.Prediction.MeasuredRTWindow)
                    continue;
                SrmDocument docWindow = document.ChangeSettings(
                    document.Settings.ChangePeptidePrediction(p => p.ChangeMeasuredRTWindow(window)));
                AddCurve(docWindow, COLORS_WINDOW[(i+1)%COLORS_WINDOW.Length]);
            }

            AxisChange();
        }

        private void AddCurve(SrmDocument document, Color color)
        {
            var predict = document.Settings.PeptideSettings.Prediction;

            // TODO: Guess this value from the document
            bool singleWindow = false;

            List<PrecursorScheduleBase> listSchedules = new List<PrecursorScheduleBase>();
            double xMax = double.MinValue, xMin = double.MaxValue;

            foreach (var nodePep in document.Peptides)
            {
                foreach (TransitionGroupDocNode nodeGroup in nodePep.Children)
                {
                    double timeWindow;
                    double? retentionTime = predict.PredictRetentionTime(nodePep, nodeGroup,
                        singleWindow, out timeWindow);
                    if (retentionTime.HasValue)
                    {
                        var schedule = new PrecursorScheduleBase(nodeGroup, retentionTime.Value, timeWindow, 0);
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

            string label = string.Format("{0} Minute Window",
                                         document.Settings.PeptideSettings.Prediction.MeasuredRTWindow);
            var curve = AddCurve(label, points, color);
            curve.Line.IsAntiAlias = true;
            curve.Line.IsOptimizedDraw = true;
            // TODO: Give this graph its own line width
            curve.Line.Width = Settings.Default.ChromatogramLineWidth;
            curve.Symbol.IsVisible = false;
        }
    }
}