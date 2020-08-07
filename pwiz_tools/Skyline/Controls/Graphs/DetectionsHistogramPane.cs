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

        public override ImmutableList<int> GetDataSeries()
        {
            return TargetData.Histogram;
        }


        public override void UpdateGraph(bool selectionChanged)
        {
            GraphObjList.Clear();
            CurveList.Clear();
            Legend.IsVisible = false;

            if (!DetectionPlotData.DataCache.TryGet(
                GraphSummary.DocumentUIContainer.DocumentUI, Settings.QValueCutoff, this.DataCallback,
                out _detectionData))
                return;
            AddLabels();

            BarSettings.Type = BarType.SortedOverlay;
            BarSettings.MinClusterGap = 0.3f;

            //draw bars
            var countPoints = new PointPairList(Enumerable.Range(0, _detectionData.ReplicateCount)
                .Select(i => new PointPair(i, TargetData.Histogram[i] / YScale)).ToList());
            CurveList.Insert(0, MakeBarItem(countPoints, Color.FromArgb(180, 220, 255)));

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
