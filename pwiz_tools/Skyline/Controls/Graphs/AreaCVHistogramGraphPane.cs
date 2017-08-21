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
    public interface IAreaCVHistogramInfo    // CONSIDER: Base class instead?
    {
        int Objects { get; }
        AreaCVGraphData CurrentData { get; }
        AreaCVGraphData.AreaCVGraphDataCache Cache { get; }
    }

    public class AreaCVHistogramGraphPane : SummaryGraphPane, IDisposable, IAreaCVHistogramInfo
    {
        private readonly AreaCVGraphData.AreaCVGraphDataCache _cache;
        private AreaCVGraphData _areaCVGraphData = AreaCVGraphData.INVALID;
        private AreaCVGraphData.CVData _selectedData;
        private SrmDocument _document;
        private bool _percentage;
        private int _decimals;

        private readonly List<StickItem> _stickItems;

        public AreaCVHistogramGraphPane(GraphSummary graphSummary)
            : base(graphSummary)
        {
            _stickItems = new List<StickItem>(2);
            _cache = new AreaCVGraphData.AreaCVGraphDataCache();
        }

        public AreaCVGraphData.AreaCVGraphDataCache Cache { get { return _cache; } }

        public int Objects { get { return GetBoxObjCount(); } }

        public override bool HasToolbar { get { return true; } }

        public override void OnClose(EventArgs e)
        {
            Dispose();
        }

        public void Dispose()
        {
            _cache.Dispose();
        }

        public override bool HandleMouseMoveEvent(ZedGraphControl sender, MouseEventArgs e)
        {
            using (var g = sender.CreateGraphics())
            {
                object nearestObject;
                int index;
                if (FindNearestObject(e.Location, g, out nearestObject, out index) && nearestObject is BoxObj)
                {
                    _selectedData = (AreaCVGraphData.CVData)((BoxObj) nearestObject).Tag;
                    sender.Cursor = Cursors.Hand;
                    return true;
                }
                else
                {
                    _selectedData = null;
                    sender.Cursor = Cursors.Cross;
                    return base.HandleMouseMoveEvent(sender, e);
                }
            }
        }

        public override void HandleMouseClick(object sender, MouseEventArgs e)
        {
            if (_selectedData != null && e.Button == MouseButtons.Left)
            {
                HistogramHelper.CreateAndShowFindResults((ZedGraphControl) sender, _document, _selectedData);
                _selectedData = null;
            }
        }

        public override void Draw(Graphics g)
        {
            GraphObjList.RemoveAll(o => !(o is BoxObj));

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
            if (!GraphSummary.DocumentUIContainer.DocumentUI.Settings.HasResults)
            {
                _areaCVGraphData = null;
                return;
            }

             var settings = new AreaCVGraphData.AreaCVGraphSettings();
            _document = GraphSummary.DocumentUIContainer.DocumentUI;

            _percentage = !Settings.Default.AreaCVShowDecimals;

            var factor = AreaGraphController.GetAreaCVFactorToDecimal();
            _decimals = _percentage ? 1 : 3;

            GraphObjList.Clear();
            CurveList.Clear();
            _stickItems.Clear();

            var gotData = _cache.TryGet(_document, settings, DataCallback, out _areaCVGraphData);

            if (!gotData || !_areaCVGraphData.IsValid)
                return;

            var fontHeight = GraphSummary.CreateFontSpec(Color.Black).GetHeight(CalcScaleFactor());
            var height = PaneHeightToYValue(fontHeight);

            var heightFactor = 1;
            if (Settings.Default.AreaCVShowMedianCV)
            {
                var stick = AddStickItem(_areaCVGraphData.MedianCV * factor, _areaCVGraphData.MedianCV * factor, 0.0, _areaCVGraphData.MaxFrequency + heightFactor++ * height, Color.Blue);
                CurveList.Add(stick);
               _stickItems.Add(stick);
            }

            if (Settings.Default.AreaCVShowCVCutoff)
            {
                var stick = AddStickItem(Settings.Default.AreaCVCVCutoff, Settings.Default.AreaCVCVCutoff, 0.0, _areaCVGraphData.MaxFrequency + heightFactor++ * height, Color.Red);
                CurveList.Add(stick);
                _stickItems.Add(stick);
            }

            var selected = HistogramHelper.GetSelectedPeptides(GraphSummary).NodePeps.OrderBy(p => p.Id.GlobalIndex).ToList();
            var comparer = Comparer<PeptideDocNode>.Create((a, b) => a.Id.GlobalIndex.CompareTo(b.Id.GlobalIndex));

            foreach (var d in _areaCVGraphData.Data)
            {
                int frequency = 0;
                var x = d.CV * factor;

                if (Settings.Default.ShowReplicateSelection)
                {
                    frequency = d.PeptideAnnotationPairs.Count(pair => selected.BinarySearch(pair.Peptide, comparer) >= 0);
                    
                    // Add a red box, representing the number of selected peptides that contributed to this CV
                    if (frequency > 0)
                        GraphObjList.Add(new BoxObj(x, frequency, Settings.Default.AreaCVHistogramBinWidth, frequency,
                            Color.Black, Color.Red)
                        {
                            ZOrder = ZOrder.E_BehindCurves,
                            IsClippedToChartRect = true,
                            Tag = d
                        });
                }

                GraphObjList.Add(new BoxObj(x, d.Frequency, Settings.Default.AreaCVHistogramBinWidth,
                    d.Frequency - frequency, Color.Black,
                    frequency > 0 ? Color.FromArgb(Color.Red.ToArgb() & 0x7FFFFFFF) : Color.FromArgb(180, 220, 255))
                {
                    ZOrder = ZOrder.E_BehindCurves,
                    IsClippedToChartRect = true,
                    Tag = d
                });
            }

            XAxis.Title.Text = Resources.AreaCVHistogramGraphPane_UpdateGraph_CV + (_percentage ? " (%)" : string.Empty); // Not L10N
            YAxis.Title.Text = Resources.AreaCVHistogramGraphPane_UpdateGraph_Frequency;

            XAxis.Scale.Min = YAxis.Scale.Min = 0;
            XAxis.Scale.MinAuto = XAxis.Scale.MaxAuto = YAxis.Scale.MinAuto = YAxis.Scale.MaxAuto = false;

            if (!double.IsNaN(Settings.Default.AreaCVMaxCV))
                XAxis.Scale.Max = Settings.Default.AreaCVMaxCV;
            else
                XAxis.Scale.Max = _areaCVGraphData.MaxCV * factor + Settings.Default.AreaCVHistogramBinWidth;

            if (!double.IsNaN(Settings.Default.AreaCVMaxFrequency))
                YAxis.Scale.Max = Settings.Default.AreaCVMaxFrequency;
            else
                YAxis.Scale.Max = _areaCVGraphData.MaxFrequency + heightFactor * height;

            AxisChange();
        }

        private void AddLabels(Graphics g)
        {
            if (_areaCVGraphData == null)
            {
                Title.Text = Resources.AreaCVHistogramGraphPane_AddLabels_Calculating____;
            }
            else if (!_areaCVGraphData.IsValid)
            {
                Title.Text = Resources.AreaCVHistogramGraphPane_AddLabels_Not_enough_data;
            }
            else
            {
                Title.Text = string.Empty;
                
                var unit = _percentage ? "%" : string.Empty; // Not L10N
                var factor = _percentage ? 100.0 : 1.0;

                var scaleFactor = CalcScaleFactor();
                var fontHeight = GraphSummary.CreateFontSpec(Color.Black).GetHeight(scaleFactor);
                var height = PaneHeightToYValue(fontHeight);
                // Anchor labels at top of graph pane
                var y = Math.Min(PaneHeightToYValue(Rect.Height - TitleGap * Title.FontSpec.GetHeight(scaleFactor) - fontHeight * _stickItems.Count), _areaCVGraphData.MaxFrequency + height);

                var index = 0;
                if (Settings.Default.AreaCVShowMedianCV)
                {
                    _stickItems[index++].Points[1].Y = y;
                    string text = string.Format(Resources.AreaCVHistogramGraphPane_AddLabels_Median___0_,
                        HistogramHelper.FormatDouble(_areaCVGraphData.MedianCV * factor, _decimals) + unit);
                    GraphObjList.Add(AddLabel(text, _areaCVGraphData.MedianCV * factor, y, Color.Blue));
                    y += height;      
                }

                if (Settings.Default.AreaCVShowCVCutoff)
                {
                    _stickItems[index++].Points[1].Y = y;
                    string text = string.Format(Resources.AreaCVHistogramGraphPane_UpdateGraph_Below__0____1_,
                            Settings.Default.AreaCVCVCutoff + unit, HistogramHelper.FormatDouble(_areaCVGraphData.BelowCVCutoff * factor, _decimals) + unit);
                    GraphObjList.Add(AddLabel(text, Settings.Default.AreaCVCVCutoff, y, Color.Red));
                }
            }
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

        #region Functional Test Support

        public AreaCVGraphData CurrentData { get { return _areaCVGraphData; } }

        #endregion
    }
}