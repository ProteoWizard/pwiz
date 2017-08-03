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
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using ZedGraph;

namespace pwiz.Skyline.Controls.Graphs
{
    public class AreaCVHistogramGraphPane : SummaryGraphPane
    {
        private AreaCVGraphData.AreaCVGraphDataCache _cache;
        private AreaCVGraphData _areaCVGraphData;
        private SrmDocument _document;
        private bool _percentage;
        private int _decimals;

        private readonly List<StickItem> _stickItems;

        public AreaCVGraphData.AreaCVGraphDataCache Cache { get { return _cache; } }

        public AreaCVHistogramGraphPane(GraphSummary graphSummary)
            : base(graphSummary)
        {
            _stickItems = new List<StickItem>(2);
            _cache = new AreaCVGraphData.AreaCVGraphDataCache(new AreaCVGraphData.AreaCVGraphSettings());
        }

        public override void Draw(Graphics g)
        {
            GraphObjList.Clear();

            YAxis.Scale.Min = 0.0;

            AxisChange(g);
            AddLabels(g);

            base.Draw(g);
        }

        public override bool HasToolbar { get { return true; } }

        private double PaneHeightToYValue(double height)
        {
            return height * (YAxis.Scale.Max - YAxis.Scale.Min) / Rect.Height;
        }

        public override void UpdateGraph(bool selectionChanged)
        {
            if (!GraphSummary.DocumentUIContainer.DocumentUI.Settings.HasResults)
            {
                _areaCVGraphData = null;
                return;
            }

            if(!double.IsNaN(Settings.Default.AreaCVQValueCutoff) && !GraphSummary.DocumentUIContainer.DocumentUI.Settings.PeptideSettings.Integration.PeakScoringModel.IsTrained)
                Settings.Default.AreaCVQValueCutoff = double.NaN;
            
            var settings = new AreaCVGraphData.AreaCVGraphSettings();

            var settingsChanged = !_cache.IsValidFor(settings);
            if (_document != null && !ReferenceEquals(_document.Children, GraphSummary.DocumentUIContainer.DocumentUI.Children) || settingsChanged)
            {
                var isCaching = _cache.IsCaching;
                _cache.Cancel();
                if (isCaching || settingsChanged)
                    _cache = new AreaCVGraphData.AreaCVGraphDataCache(settings);
            }

            _document = GraphSummary.DocumentUIContainer.DocumentUI;

            _percentage = !Settings.Default.AreaCVShowDecimals;

            var factor = AreaGraphController.GetAreaCVFactorToDecimal();
            _decimals = _percentage ? 1 : 3;

            _areaCVGraphData = _cache.Get(AreaGraphController.GroupByGroup,
                                                                AreaGraphController.GroupByAnnotation,
                                                                AreaGraphController.MinimumDetections,
                                                                AreaGraphController.NormalizationMethod,
                                                                AreaGraphController.AreaCVRatioIndex);
            if (_areaCVGraphData == null)
            {
                _areaCVGraphData = new AreaCVGraphData(GraphSummary.DocumentUIContainer.DocumentUI, new AreaCVGraphData.AreaCVGraphSettings());

                _cache.Add(_areaCVGraphData);
                _cache.CacheRemaining(GraphSummary.DocumentUIContainer.DocumentUI);
            }

            CurveList.Clear();
            _stickItems.Clear();

            if (!_areaCVGraphData.IsValid)
                return;

            BarSettings.ClusterScaleWidth = Settings.Default.AreaCVHistogramBinWidth;
            BarSettings.MinClusterGap = 0;

            var fontHeight = GraphSummary.CreateFontSpec(Color.Black).GetHeight(CalcScaleFactor());
            var height = PaneHeightToYValue(fontHeight);

            if (Settings.Default.AreaCVShowMedianCV)
            {
                var stick = AddStickItem(_areaCVGraphData.MedianCV * factor, _areaCVGraphData.MedianCV * factor, 0.0, _areaCVGraphData.MaxFrequency + height, Color.Blue);
                CurveList.Add(stick);
               _stickItems.Add(stick);
            }

            if (Settings.Default.AreaCVShowCVCutoff)
            {
                var stick = AddStickItem(Settings.Default.AreaCVCVCutoff, Settings.Default.AreaCVCVCutoff, 0.0, _areaCVGraphData.MaxFrequency + height, Color.Red);
                CurveList.Add(stick);
                _stickItems.Add(stick);
            }

            var ps = new PointPairList(
                _areaCVGraphData.Data.Select(d => d.CV * factor + Settings.Default.AreaCVHistogramBinWidth / 2.0).ToList(),
                _areaCVGraphData.Data.Select(d => (double) d.Frequency).ToList());
            var bar = new BarItem(null, ps, Color.FromArgb(180, 220, 255));
            bar.Bar.Fill.Type = FillType.Solid;
            CurveList.Add(bar);

            XAxis.Title.Text = Resources.AreaCVHistogramGraphPane_UpdateGraph_CV + (_percentage ? " (%)" : string.Empty); // Not L10N
            YAxis.Title.Text = Resources.AreaCVHistogramGraphPane_UpdateGraph_Frequency;

            XAxis.Scale.Min = YAxis.Scale.Min = 0;
            XAxis.Scale.MinAuto = XAxis.Scale.MaxAuto = YAxis.Scale.MinAuto = YAxis.Scale.MaxAuto = false;

            if (!double.IsNaN(Settings.Default.AreaCVMaxCV))
                XAxis.Scale.Max = Settings.Default.AreaCVMaxCV;
            else
                XAxis.Scale.MaxAuto = true;

            if (!double.IsNaN(Settings.Default.AreaCVMaxFrequency))
                YAxis.Scale.Max = Settings.Default.AreaCVMaxFrequency;
            else
                YAxis.Scale.MaxAuto = true;

            AxisChange();
        }

        private void AddLabels(Graphics g)
        {
            if (_areaCVGraphData == null || !_areaCVGraphData.IsValid)
            {
                Title.Text = Resources.AreaCVHistogramGraphPane_AddLabels_Not_enough_data;		        
            }
            else
            {
                Title.Text = string.Empty;
                
                var unit = _percentage ? "%" : string.Empty; // Not L10N
                var factor = _percentage ? 100.0 : 1.0;

                var fontHeight = GraphSummary.CreateFontSpec(Color.Black).GetHeight(CalcScaleFactor());
                var height = PaneHeightToYValue(fontHeight);
                var y = Math.Min(PaneHeightToYValue(Rect.Height - fontHeight * _stickItems.Count), _areaCVGraphData.MaxFrequency + height);

                var index = 0;
                if (Settings.Default.AreaCVShowMedianCV)
                {
                    _stickItems[index++].Points[1].Y = y;
                    string text = string.Format(Resources.AreaCVHistogramGraphPane_AddLabels_Median___0_,
                        FormatDouble(_areaCVGraphData.MedianCV * factor, _decimals) + unit);
                    GraphObjList.Add(AddLabel(text, _areaCVGraphData.MedianCV * factor, y, Color.Blue));
                    y += height;      
                }

                if (Settings.Default.AreaCVShowCVCutoff)
                {
                    _stickItems[index++].Points[1].Y = y;
                    string text = string.Format(Resources.AreaCVHistogramGraphPane_UpdateGraph_Below__0____1_,
                            Settings.Default.AreaCVCVCutoff + unit, FormatDouble(_areaCVGraphData.belowCVCutoff * factor, _decimals) + unit);
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

        public static string FormatDouble(double d, int decimals)
        {
            return d.ToString("F0" + decimals, LocalizationHelper.CurrentCulture); // Not L10N
        }

        #region Functional Test Support

        public AreaCVGraphData CurrentData { get { return _areaCVGraphData; } }

        #endregion
    }
}