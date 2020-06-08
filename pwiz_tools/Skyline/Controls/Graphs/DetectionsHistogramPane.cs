using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using pwiz.Common.Collections;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model;
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
            if (!_detectionData.IsValid)
                return;
            _detectionData = DetectionPlotData.DataCache.Get(GraphSummary.DocumentUIContainer.DocumentUI);

            BarSettings.Type = BarType.SortedOverlay;
            BarSettings.MinClusterGap = 0.3f;

            GraphObjList.Clear();
            CurveList.Clear();
            Legend.IsVisible = false;
            //draw bars
            var countPoints = new PointPairList(Enumerable.Range(0, _detectionData.ReplicateCount)
                .Select(i => new PointPair(i, TargetData.Histogram[i] / YScale)).ToList());
            CurveList.Insert(0, MakeBarItem(countPoints, Color.FromArgb(180, 220, 255)));

            //axes formatting
            XAxis.Scale.Max = _detectionData.ReplicateCount + 1;

            YAxis.Scale.Max = TargetData.Histogram.Max() / YScale * 1.15;
        }

        public void Dispose()
        {
            _detectionData.Dispose();
        }

        protected override void PopulateTooltip(int index)
        {
            ToolTip.ClearData();
            DetectionPlotData.DataSet targetData = _detectionData.GetData(Settings.TargetType);

            ToolTip.AddLine(Resources.DetectionHistogramPane_Tooltip_ReplicateCount,
                index.ToString( CultureInfo.CurrentCulture));
            ToolTip.AddLine(String.Format(Resources.DetectionHistogramPane_Tooltip_Count, Settings.TargetType.Label),
                targetData.Histogram[index].ToString(CultureInfo.CurrentCulture));

        }
        protected override void HandleMouseClick(int index) { }

        protected override void AddLabels(Graphics g)
        {
            base.AddLabels(g);
            YAxis.Title.Text = Resources.DetectionHistogramPane_YAxis_Name;
            if (Settings.YScaleFactor != DetectionsGraphController.YScaleFactorType.ONE)
                YAxis.Title.Text += @" (" + Settings.YScaleFactor.Label + @")";
        }

        #region Functional Test Support


        #endregion
    }
}
