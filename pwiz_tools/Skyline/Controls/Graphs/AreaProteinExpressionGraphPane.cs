using System.Collections.Generic;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Controls.Graphs
{
    // TODO make private
    public class AreaProteinExpressionGraphPane : SummaryProteinExpressionGraphPane
    {
        public AreaProteinExpressionGraphPane(GraphSummary graphSummary, PaneKey paneKey)
            : base(graphSummary, paneKey)
        {
        }
        protected override GraphData CreateGraphData(PeptideGroupDocNode selectedProtein, List<ProteinAbundanceResult> results)
        {
            int? result = null;
            if (RTLinearRegressionGraphPane.ShowReplicate == ReplicateDisplay.single)
            {
                result = GraphSummary.ResultsIndex;
            }
            
            return new AreaGraphData(selectedProtein, result, results);
        }

        protected override void UpdateAxes()
        {
            YAxis.Title.Text = Resources.AreaPeptideGraphPane_UpdateAxes_Peak_Area;

            base.UpdateAxes();
            // reformat YAxis for labels
            var maxY = GraphHelper.GetMaxY(CurveList, this);
            GraphHelper.ReformatYAxis(this, maxY);

        }

        internal class AreaGraphData : GraphData
        {
            public AreaGraphData(
                PeptideGroupDocNode selectedProtein,
                int? result, List<ProteinAbundanceResult> results)
                : base(selectedProtein, result, results)
            {
            }

            public override double MaxValueSetting { get { return Settings.Default.PeakAreaMaxArea; } }
            public override double MaxCvSetting { get { return Settings.Default.PeakAreaMaxCv; } }

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
