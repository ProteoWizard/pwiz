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
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using pwiz.MSGraph;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using ZedGraph;

namespace pwiz.Skyline.Controls.Graphs
{
    public class AreaCVHistogram2DGraphPane : SummaryGraphPane
    {
        private AreaCVGraphData.AreaCVGraphDataCache _cache;
        private AreaCVGraphData _areaCVGraphData;
        private SrmDocument _document;

        private readonly LineItem[] _lineItems;
        private bool _percentage;
        private int _decimals;

        public AreaCVGraphData.AreaCVGraphDataCache Cache { get { return _cache; } }

        public AreaCVHistogram2DGraphPane(GraphSummary graphSummary)
            : base(graphSummary)
        {
            _areaCVGraphData = null;
            _lineItems = new LineItem[2];
            _cache = new AreaCVGraphData.AreaCVGraphDataCache(new AreaCVGraphData.AreaCVGraphSettings());
        }

        public override bool HasToolbar { get { return true; } }

        public override void Draw(Graphics g)
        {
            GraphObjList.Clear();

            YAxis.Scale.Min = 0.0;

            AxisChange(g);

            foreach (var l in _lineItems)
            {
                if (l != null)
                {
                    l[0].X = XAxis.Scale.Min;
                    l[1].X = XAxis.Scale.Max;
                }
            }

            base.Draw(g);
        }

        public override void UpdateGraph(bool selectionChanged)
        {
            if (!GraphSummary.DocumentUIContainer.DocumentUI.Settings.HasResults)
            {
                _areaCVGraphData = null;
                return;
            }

            if (!double.IsNaN(Settings.Default.AreaCVQValueCutoff) && !GraphSummary.DocumentUIContainer.DocumentUI.Settings.PeptideSettings.Integration.PeakScoringModel.IsTrained)
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
            _decimals = _percentage ? 1 : 3;

            var factor = AreaGraphController.GetAreaCVFactorToPercentage();

            _areaCVGraphData = _cache.Get(AreaGraphController.GroupByGroup, AreaGraphController.GroupByAnnotation,
                AreaGraphController.MinimumDetections, AreaGraphController.NormalizationMethod, AreaGraphController.AreaCVRatioIndex);
            if (_areaCVGraphData == null)
            {
                _areaCVGraphData = new AreaCVGraphData(GraphSummary.DocumentUIContainer.DocumentUI, settings);

                _cache.Add(_areaCVGraphData);
                _cache.CacheRemaining(GraphSummary.DocumentUIContainer.DocumentUI);
            }

            CurveList.Clear();
            if (!_areaCVGraphData.IsValid)
            {
                Title.Text = Resources.AreaCVHistogram2DGraphPane_Draw_Not_enough_data;
                return;
            }

            Title.Text = string.Empty;

            YAxis.Title.Text = Resources.AreaCVHistogram2DGraphPane_UpdateGraph_CV + (_percentage ? " (%)" : string.Empty); // Not L10N
            XAxis.Title.Text = Resources.AreaCvHistogram2DGraphPane_UpdateGraph_Log10_Mean_Area;

            XAxis.Scale.MinAuto = XAxis.Scale.MinAuto = XAxis.Scale.MaxAuto = YAxis.Scale.MaxAuto = false;
            XAxis.Scale.Min = Math.Max(0, double.IsNaN(Settings.Default.AreaCVMinLog10Area) ? _areaCVGraphData.MinMeanArea : Settings.Default.AreaCVMinLog10Area);
            XAxis.Scale.Max = double.IsNaN(Settings.Default.AreaCVMaxLog10Area) ? _areaCVGraphData.MaxMeanArea : Settings.Default.AreaCVMaxLog10Area;
            YAxis.Scale.Min = 0.0;
            YAxis.Scale.Max = double.IsNaN(Settings.Default.AreaCVMaxCV) ? _areaCVGraphData.MaxCV * factor : Settings.Default.AreaCVMaxCV;

            AxisChange();

            var heatMapData = new HeatMapData(_areaCVGraphData.Data.Select(d => new Point3D(d.MeanArea, d.CV * factor, d.Frequency)).ToList());
            HeatMapGraphPane.GraphHeatMap(this, heatMapData, 17, 2, (float)(_areaCVGraphData.MinCV * factor), (float)(_areaCVGraphData.MaxCV * factor), Settings.Default.AreaCVLogScale, 0);

            var unit = _percentage ? "%" : string.Empty; // Not L10N

            if (Settings.Default.AreaCVShowMedianCV)
            {
                string text = string.Format(Resources.AreaCVHistogram2DGraphPane_UpdateGraph_Median___0_, AreaCVHistogramGraphPane.FormatDouble(_areaCVGraphData.MedianCV * factor, _decimals) + unit);
                _lineItems[0] = AddLineItem(text, XAxis.Scale.Min, XAxis.Scale.Max, _areaCVGraphData.MedianCV * factor, _areaCVGraphData.MedianCV * factor, Color.Blue);
                CurveList.Insert(0, _lineItems[0]);
            }

            if (Settings.Default.AreaCVShowCVCutoff)
            {
                string text = string.Format(Resources.AreaCVHistogramGraphPane_UpdateGraph_Below__0____1_, Settings.Default.AreaCVCVCutoff + unit,
                              AreaCVHistogramGraphPane.FormatDouble(_areaCVGraphData.belowCVCutoff * factor, _decimals) +
                              unit);
                _lineItems[1] = AddLineItem(text,XAxis.Scale.Min, XAxis.Scale.Max,Settings.Default.AreaCVCVCutoff, Settings.Default.AreaCVCVCutoff, Color.Red);
                CurveList.Insert(0, _lineItems[1]);
            }
        }

        private LineItem AddLineItem(string text, double fromX, double fromY, double toX, double toY, Color color)
        {
            return new LineItem(text, new[] { fromX, fromY }, new[] { toX, toY }, color, SymbolType.None, 2.0f)
            { Line = { Style = DashStyle.Dash } };
        }

        #region Functional Test Support

        public AreaCVGraphData CurrentData { get { return _areaCVGraphData; } }

        #endregion
    }
}