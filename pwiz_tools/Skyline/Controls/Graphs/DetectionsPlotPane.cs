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
using System.Reflection;
using System.Windows.Forms;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
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
        public class IntLabeledValue : LabeledValues<int>
        {
            protected IntLabeledValue(int value, Func<string> getLabelFunc) : base(value, getLabelFunc)
            {
                Value = value;
            }
            public float Value { get; private set; }

            public override string ToString()
            {
                return Label;
            }

            public static IEnumerable<T> GetValues<T>() where T : IntLabeledValue
            {
                return (IEnumerable<T>)typeof(T).InvokeMember("GetValues", BindingFlags.InvokeMethod, 
                    null, null, new object[0]);
            }

            public static T GetDefaultValue<T>() where T : IntLabeledValue
            {
                return (T)typeof(T).InvokeMember("GetDefaultValue", BindingFlags.InvokeMethod,
                    null, null, new object[0]);
            }

            public static T GetFromString<T>(string str) where T : IntLabeledValue
            {
                var res = GetValues<T>().FirstOrDefault(
                    (t) => t.Label.Equals(str));
                if (res == default(T))
                    return GetDefaultValue<T>();
                else return res;
            }

            public static void PopulateCombo<T>(ComboBox comboBox, T currentValue) where T : IntLabeledValue
            {
                comboBox.Items.Clear();
                foreach (var val in GetValues<T>())
                {
                    comboBox.Items.Add(val);
                    if (Equals(val, currentValue))
                    {
                        comboBox.SelectedIndex = comboBox.Items.Count - 1;
                    }
                }
            }
            public static void PopulateCombo<T>(ToolStripComboBox comboBox, T currentValue) where T : IntLabeledValue
            {
                comboBox.Items.Clear();
                foreach (var val in GetValues<T>())
                {
                    comboBox.Items.Add(val);
                    if (Equals(val, currentValue))
                    {
                        comboBox.SelectedIndex = comboBox.Items.Count - 1;
                    }
                }
            }

            public static T GetValue<T>(ComboBox comboBox, T defaultVal) where T : IntLabeledValue
            {
                return comboBox.SelectedItem as T ?? defaultVal;
            }
            public static T GetValue<T>(ToolStripComboBox comboBox, T defaultVal) where T : IntLabeledValue
            {
                return comboBox.SelectedItem as T ?? defaultVal;
            }
        }

        public class TargetType : IntLabeledValue
        {
            private TargetType(int value, Func<string> getLabelFunc) : base(value, getLabelFunc){}

            public static readonly TargetType PRECURSOR = new TargetType(0, () => Resources.DetectionPlot_TargetType_Precursor);
            public static readonly TargetType PEPTIDE = new TargetType(1, () => Resources.DetectionPlot_TargetType_Peptide);

            public static IEnumerable<TargetType> GetValues()
            {
                return new[] {PRECURSOR, PEPTIDE};
            }

            public static TargetType GetDefaultValue()
            {
                return PRECURSOR;
            }
        }

        public class YScaleFactorType : IntLabeledValue
        {
            private YScaleFactorType(int value, Func<string> getLabelFunc) : base(value, getLabelFunc) { }

            public static readonly YScaleFactorType ONE = new YScaleFactorType(1, () => Resources.DetectionPlot_YScale_One);
            public static readonly YScaleFactorType HUNDRED = new YScaleFactorType(100, () => Resources.DetectionPlot_YScale_Hundred);
            public static readonly YScaleFactorType THOUSAND = new YScaleFactorType(100, () => Resources.DetectionPlot_YScale_Thousand);

            public static IEnumerable<YScaleFactorType> GetValues()
            {
                return new[] { ONE, HUNDRED, THOUSAND };
            }
            public static YScaleFactorType GetDefaultValue()
            {
                return THOUSAND;
            }
        }


        //        public enum TargetType { precursor, peptide }
        //public enum YScaleFactorType { one = 1, hundreds = 100, thousands = 1000}

        public class Settings
        {
            public static float QValueCutoff
            {
                get { return pwiz.Skyline.Properties.Settings.Default.DetectionsQValueCutoff; }
                set { pwiz.Skyline.Properties.Settings.Default.DetectionsQValueCutoff = value; }
            }

            public static TargetType TargetType
            {
                get
                {
                    return IntLabeledValue.GetFromString<TargetType>(
                        pwiz.Skyline.Properties.Settings.Default.DetectionsTargetType);
                }
                set { pwiz.Skyline.Properties.Settings.Default.DetectionsTargetType = value.ToString(); }
            }
            public static YScaleFactorType YScaleFactor
            {
                get
                {
                    return IntLabeledValue.GetFromString<YScaleFactorType>(
                        pwiz.Skyline.Properties.Settings.Default.DetectionsYScaleFactor);
                }
                set { pwiz.Skyline.Properties.Settings.Default.DetectionsYScaleFactor = value.ToString(); }
            }

            public static int RepCount
            {
                get { return pwiz.Skyline.Properties.Settings.Default.DetectionsRepCount; }
                set { pwiz.Skyline.Properties.Settings.Default.DetectionsRepCount = value; }
            }

            public static float FontSize
            {
                get { return pwiz.Skyline.Properties.Settings.Default.AreaFontSize; }
                set { pwiz.Skyline.Properties.Settings.Default.AreaFontSize = value; }
            }

            public static bool ShowAtLeastN
            {
                get { return pwiz.Skyline.Properties.Settings.Default.DetectionsShowAtLeastN; }
                set { pwiz.Skyline.Properties.Settings.Default.DetectionsShowAtLeastN = value; }
            }

            public static bool ShowSelection
            {
                get { return pwiz.Skyline.Properties.Settings.Default.DetectionsShowSelection; }
                set { pwiz.Skyline.Properties.Settings.Default.DetectionsShowSelection = value; }
            }

            public static bool ShowMean
            {
                get { return pwiz.Skyline.Properties.Settings.Default.DetectionsShowMean; }
                set { pwiz.Skyline.Properties.Settings.Default.DetectionsShowMean = value; }
            }

        }

        private DetectionPlotData _detectionData = DetectionPlotData.INVALID;
        public int MaxRepCount { get; private set; }

        public static int DefaultMaxRepCount => 20;

        private readonly List<StickItem> _stickItems;

        public DetectionsPlotPane(GraphSummary graphSummary) : base(graphSummary)
        {
            MaxRepCount = graphSummary.DocumentUIContainer.DocumentUI.MeasuredResults.Chromatograms.Count;

            Settings.RepCount = (int) MaxRepCount / 2;
            if (GraphSummary.Toolbar is DetectionsToolbar toolbar)
                toolbar.UpdateUI();

            _stickItems = new List<StickItem>(2);
            if (GraphSummary.DocumentUIContainer.DocumentUI.Settings.HasResults)
            {
                _detectionData = new DetectionPlotData(graphSummary.DocumentUIContainer.DocumentUI);
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

            if (Settings.ShowAtLeastN)
            {
                DetectionPlotData.DataSet targetData = _detectionData.GetData(Settings.TargetType);
                float yScale = (float)Settings.YScaleFactor.Value;
                double lineY = targetData.getCountForMinReplicates(Settings.RepCount) / yScale;
                
                var lineYDevice = YAxis.Scale.Transform(lineY);
                using (var pen = new Pen(Color.Blue, 1){DashStyle = DashStyle.DashDot})
                {
                    g.DrawLine(pen, Chart.Rect.Left, lineYDevice, Chart.Rect.Right, lineYDevice);
                }
            }
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
                var oldCutoff = Settings.QValueCutoff;
                if(Settings.QValueCutoff != oldCutoff)
                    _detectionData = new DetectionPlotData(GraphSummary.DocumentUIContainer.DocumentUI);
            }
            else
                return;

            BarSettings.Type = BarType.SortedOverlay;
            BarSettings.MinClusterGap = 0.15f;

            GraphObjList.Clear();
            CurveList.Clear();
            _stickItems.Clear();

            DetectionPlotData.DataSet targetData = _detectionData.GetData(Settings.TargetType);
            //draw bars
            float yScale = (float) Settings.YScaleFactor.Value;
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

            YAxis.Scale.Max = _detectionData.GetData(Settings.TargetType).MaxCount/yScale * 1.05;
            if (Settings.YScaleFactor != YScaleFactorType.ONE)
                YAxis.Title.Text = $"Detections ({Settings.YScaleFactor.ToString()})";
            else
                YAxis.Title.Text = "Detections";

            if (Settings.ShowAtLeastN)
            {
                //This is a placeholder to make sure the line shows in the legend.
                //Actual drawing happens in the Draw method because ZedGraph doesn't allow
                // to draw end to end line for an ordinal axis
                var linePoints = new PointPairList( new[] {new PointPair(0, 0)} );
                var line = new LineItem(
                        string.Format("at least {0}", Settings.RepCount),
                        linePoints, Color.Blue, SymbolType.None)
                    {Line = {Style = DashStyle.DashDot}};
                CurveList.Insert(3, line);
            }
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
