/*
 * Original author: Rita Chupalov <ritach .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2020 University of Washington - Seattle, WA
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
using System.Globalization;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Skyline.Properties;
using ZedGraph;
using Settings = pwiz.Skyline.Controls.Graphs.DetectionsGraphController.Settings;

namespace pwiz.Skyline.Controls.Graphs
{
    public class DetectionsHistogramPane : DetectionsPlotPane
    {

        public DetectionsHistogramPane(GraphSummary graphSummary) : base(graphSummary )
        {
            XAxis.Type = AxisType.Ordinal;
            XAxis.Title.Text = Resources.DetectionHistogramPane_XAxis_Name;
        }

        public override ImmutableList<float> GetToolTipDataSeries()
        {
            return ImmutableList.ValueOf(TargetData.Histogram.Select(n => (float)n));
        }


        public override void UpdateGraph(bool selectionChanged)
        {
            GraphObjList.Clear();
            CurveList.Clear();
            Legend.IsVisible = false;

            if (!DetectionPlotData.GetDataCache().TryGet(
                GraphSummary.DocumentUIContainer.DocumentUI, Settings.QValueCutoff, this.DataCallback,
                out _detectionData))
                return;
            AddLabels();

            BarSettings.Type = BarType.SortedOverlay;
            BarSettings.MinClusterGap = 0.3f;

            //draw bars
            var countPoints = new PointPairList(Enumerable.Range(0, _detectionData.ReplicateCount)
                .Select(i => new PointPair(i, TargetData.Histogram[i] / YScale)).ToList());
            ToolTip.TargetCurves.ClearAndAdd(MakeBarItem(countPoints, Color.FromArgb(180, 220, 255)));
            CurveList.Insert(0, ToolTip.TargetCurves[0]);

            //axes formatting
            XAxis.Scale.Max = _detectionData.ReplicateCount + 1;

            YAxis.Scale.Max = TargetData.Histogram.Max() / YScale * 1.15;
        }

        public override void PopulateTooltip(int index)
        {
            ToolTip.ClearData();
            DetectionPlotData.DataSet targetData = _detectionData.GetTargetData(Settings.TargetType);

            ToolTip.AddLine(Resources.DetectionHistogramPane_Tooltip_ReplicateCount,
                index.ToString( CultureInfo.CurrentCulture));
            ToolTip.AddLine(String.Format(Resources.DetectionHistogramPane_Tooltip_Count, Settings.TargetType.Label),
                targetData.Histogram[index].ToString(CultureInfo.CurrentCulture));
        }
        protected override void HandleMouseClick(int index) { }

        protected override void AddLabels()
        {
            if (_detectionData.IsValid)
            {
                YAxis.Title.Text = Resources.DetectionHistogramPane_YAxis_Name;
            }
            base.AddLabels();
        }

        #region Functional Test Support


        #endregion
    }
}
