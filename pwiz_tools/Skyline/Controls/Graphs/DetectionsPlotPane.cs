/*
 * Original author: Tobias Rohde <tobiasr .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using ZedGraph;

namespace pwiz.Skyline.Controls.Graphs
{
    public interface IDetectionsPlotInfo    // CONSIDER: Base class instead?
    {
        int Items { get; }
        DetectionPlotData CurrentData { get; }
    }

    /***
     * TODO:
     *  - read settings from toolbar and update the plot on change
     *  - implement peptide-level statistics
     *  - show selected protein counts on the graph
     *  - show horizontal lines
     *  - implement OnDocumentUpdate
     *  - implement context menu
     */

    public class DetectionsPlotPane : SummaryReplicateGraphPane, IDisposable, IDetectionsPlotInfo    // CONSIDER: Base class instead?

    {
        public enum TargetType { precursor, peptide }
        public enum YScaleFactorType { one = 1, hundreds = 100, thousands = 1000}

        public class Settings
        {
            public Settings(float qValueCutoff, TargetType targetType, YScaleFactorType yScale, int repCount)
            {
                QValueCutoff = qValueCutoff;
                TargetType = targetType;
                YScaleFactor = yScale;
                RepCount = repCount;
            }

            public float QValueCutoff { get; set; }
            public TargetType TargetType { get; set; }
            public YScaleFactorType YScaleFactor { get; set; }
            public int RepCount { get; set; }
        }

        private DetectionPlotData _detectionData = DetectionPlotData.INVALID;
        public int MaxRepCount { get; private set; }

        public static int DefaultMaxRepCount => 20;
        private static Settings _defaultSettings = new Settings(0.01f, TargetType.precursor, YScaleFactorType.thousands, 10);
        public static Settings DefaultSettings
        {
            get { return _defaultSettings; }
        }

        public Settings settings { get; private set; }

        private readonly List<StickItem> _stickItems;

        public DetectionsPlotPane(GraphSummary graphSummary) : base(graphSummary)
        {
            MaxRepCount = graphSummary.DocumentUIContainer.DocumentUI.MeasuredResults.Chromatograms.Count;

            settings = DefaultSettings;
            settings.RepCount = (int) MaxRepCount / 2;
            if (GraphSummary.Toolbar is DetectionsToolbar toolbar)
                toolbar.UpdateUI(settings, MaxRepCount);

            _stickItems = new List<StickItem>(2);
            if (GraphSummary.DocumentUIContainer.DocumentUI.Settings.HasResults)
            {
                _detectionData = new DetectionPlotData(graphSummary.DocumentUIContainer.DocumentUI, settings);
            }
            XAxis.Type = AxisType.Text;
            XAxis.Title.Text = "Replicate";

            XAxis.Scale.Min = YAxis.Scale.Min = 0;
            XAxis.Scale.MinAuto = XAxis.Scale.MaxAuto = YAxis.Scale.MinAuto = YAxis.Scale.MaxAuto = false;

        }

        public int Items { get; }

        public override bool HasToolbar { get { return true; } }

        public override void OnClose(EventArgs e)
        {
            Dispose();
        }

        public void Dispose()
        {
            _detectionData.Dispose();
        }

        public override bool HandleMouseMoveEvent(ZedGraphControl sender, MouseEventArgs e)
        {
            using (var g = sender.CreateGraphics())
            {
                object nearestObject;
                int index;
                if (FindNearestObject(e.Location, g, out nearestObject, out index) && nearestObject is BarItem)
                {
                    //_selectedData = (CVData)((BarItem)nearestObject).Points[index].Tag;
                    sender.Cursor = Cursors.Hand;
                    return true;
                }
                else
                {
                    //_selectedData = null;
                    sender.Cursor = Cursors.Cross;
                    return base.HandleMouseMoveEvent(sender, e);
                }
            }
        }

        public override void HandleMouseClick(object sender, MouseEventArgs e)
        {
            //if (_selectedData != null && e.Button == MouseButtons.Left)
            //{
            //    HistogramHelper.CreateAndShowFindResults((ZedGraphControl) sender, GraphSummary, _document, _selectedData);
            //    _selectedData = null;
            //}
        }

        public override void Draw(Graphics g)
        {
            GraphObjList.Clear();

            YAxis.Scale.Min = 0.0;

            AxisChange(g);
            AddLabels(g);

            base.Draw(g);
        } 

        private double PaneHeightToYValue(double height)
        {
            return height * (YAxis.Scale.Max - YAxis.Scale.Min) / Rect.Height;
        }

        private void DataCallback(AreaCVGraphData data)
        {
            GraphSummary.GraphControl.BeginInvoke((Action) (() => { GraphSummary.UpdateUI(); }));
        }

        public override void UpdateGraph(bool selectionChanged)
        {
            if (!_detectionData.IsValid)
                return;
            if (GraphSummary.Toolbar is DetectionsToolbar toolbar)
            {
                var newSettings = toolbar.GetSettings();
                if(settings.QValueCutoff != newSettings.QValueCutoff)
                    _detectionData = new DetectionPlotData(GraphSummary.DocumentUIContainer.DocumentUI, newSettings);
                settings = newSettings;
            }
            else
                return;

            BarSettings.Type = BarType.SortedOverlay;
            BarSettings.MinClusterGap = 0.15f;

            GraphObjList.Clear();
            CurveList.Clear();
            _stickItems.Clear();

            DetectionPlotData.DataSet targetData = _detectionData.GetData(settings.TargetType);
            //draw bars
            float yScale = (float) settings.YScaleFactor;
            var counts = targetData.TargetsCount.ToList();
            var countPoints = new PointPairList(Enumerable.Range(0, _detectionData.ReplicateCount)
                .Select(i => new PointPair(i, counts[i]/yScale)).ToList());
            CurveList.Insert(0, MakeBarItem(countPoints, Color.FromArgb(180, 220, 255)));
            //draw cumulative curve
            counts = targetData.TargetsCumulative.ToList();
            var cumulativePoints = new PointPairList(Enumerable.Range(0, _detectionData.ReplicateCount)
                .Select(i => new PointPair(i, counts[i] / yScale)).ToList());
            CurveList.Insert(1, new LineItem("cumulative", cumulativePoints, 
                    Color.Coral,
                    SymbolType.Circle));
            //draw inclusive curve
            counts = targetData.TargetsAll.ToList();
            var allPoints = new PointPairList(Enumerable.Range(0, _detectionData.ReplicateCount)
                .Select(i => new PointPair(i, counts[i] / yScale)).ToList());
            CurveList.Insert(2, new LineItem("all runs", allPoints,
                Color.Black,
                SymbolType.Circle));

            //axes formatting
            var fontHeight = GraphSummary.CreateFontSpec(Color.Black).GetHeight(CalcScaleFactor());
            var height = PaneHeightToYValue(fontHeight);

            XAxis.Scale.Max = _detectionData.ReplicateCount + 1;
            XAxis.Scale.TextLabels = _detectionData.ReplicateNames.ToArray();

            YAxis.Scale.Max = _detectionData.GetData(settings.TargetType).MaxCount/yScale * 1.05;
            if (settings.YScaleFactor != YScaleFactorType.one)
                YAxis.Title.Text = $"Detections ({settings.YScaleFactor.ToString()})";
            else
                YAxis.Title.Text = "Detections";

            double lineY = targetData.getCountForMinReplicates(settings.RepCount)/yScale;

            var linePoints = new PointPairList(Enumerable.Range(0, _detectionData.ReplicateCount)
                .Select(i => new PointPair(i, lineY)).ToList());

            var line = new LineItem(
                    string.Format("at least {0}", settings.RepCount),
                    linePoints, Color.Blue, SymbolType.None)
                { Line = { Style = DashStyle.Dash } };
            CurveList.Insert(3,line);
        }

        private BarItem MakeBarItem(PointPairList points, Color color)
        {
            return new BarItem(null, points, color) { Bar =
            {
                Fill = { Type = FillType.Solid }, Border = {InflateFactor = 0.7F}
            } };
        }

        private void AddLabels(Graphics g)
        {
            if (!_detectionData.IsValid)
                Title.Text = "No valid data for this plot.";
            else
                Title.Text = string.Empty;
        }

        private StickItem AddStickItem(double fromX, double fromY, double toX, double toY, Color color)
        {
            return new StickItem(null,
                new[] { fromX, fromY },
                new[] { toX, toY }, color, 2.0f) { Line = { Style = DashStyle.Dash } };
        }

        private TextObj AddLabel(string text, double x, double y, Color color)
        {
            return new TextObj(text, x, y, CoordType.AxisXYScale, AlignH.Center, AlignV.Bottom)
            {
                FontSpec = GraphSummary.CreateFontSpec(color),
                IsClippedToChartRect = true
            };
        }

        protected override IdentityPath GetIdentityPath(CurveItem curveItem, int barIndex)
        {
            return null;
        }

        protected override void ChangeSelection(int selectedIndex, IdentityPath identityPath)
        {
        }

        protected override int SelectedIndex => throw new NotImplementedException();

        #region Functional Test Support

        public DetectionPlotData CurrentData { get { return _detectionData; } }


        #endregion
    }
}
