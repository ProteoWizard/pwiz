using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Controls.Graphs
{
    // TODO make private
    public class AreaIntensityGraphPane : SummaryIntensityGraphPane
    {
        public AreaIntensityGraphPane(GraphSummary graphSummary, PaneKey paneKey)
            : base(graphSummary, paneKey)
        {
        }
        protected override GraphData CreateGraphData(SrmDocument document, PeptideGroupDocNode selectedProtein, DisplayTypeChrom displayType)
        {
            int? result = null;
            if (RTLinearRegressionGraphPane.ShowReplicate == ReplicateDisplay.single)
                result = GraphSummary.ResultsIndex;
            return new AreaGraphData(document, selectedProtein, result, displayType, PaneKey);
        }

        protected override void UpdateAxes()
        {
            YAxis.Title.Text = Resources.AreaPeptideGraphPane_UpdateAxes_Peak_Area;

            base.UpdateAxes();
            // reformat YAxis for labels
            var maxY = GraphHelper.GetMaxY(CurveList, this);
            GraphHelper.ReformatYAxis(this, maxY);

            FixedYMin = YAxis.Scale.Min = Settings.Default.AreaLogScale ? 1 : 0;
        }

        internal class AreaGraphData : GraphData
        {
            public AreaGraphData(SrmDocument document,
                PeptideGroupDocNode selectedProtein,
                int? result,
                DisplayTypeChrom displayType,
                PaneKey paneKey)
                : base(document, selectedProtein, result, displayType, paneKey)
            {
            }

            public override double MaxValueSetting { get { return Settings.Default.PeakAreaMaxArea; } }
            public override double MaxCVSetting { get { return Settings.Default.PeakAreaMaxCv; } }

            protected override double? GetValue(TransitionGroupChromInfo chromInfo)
            {
                return chromInfo.Area;
            }

            protected override double GetValue(TransitionChromInfo chromInfo)
            {
                return chromInfo.Area;
            }
        }
    }

}
