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
        protected override GraphData CreateGraphData(SrmDocument document, PeptideGroupDocNode selectedProtein, DisplayTypeChrom displayType, List<ProteinAbundanceBindingSource.ProteinAbundanceRow> rows)
        {
            int? result = null;
            if (RTLinearRegressionGraphPane.ShowReplicate == ReplicateDisplay.single)
            {
                result = GraphSummary.ResultsIndex;
            }
            
            return new AreaGraphData(document, selectedProtein, result, displayType, PaneKey, rows);
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
            public AreaGraphData(SrmDocument document,
                PeptideGroupDocNode selectedProtein,
                int? result,
                DisplayTypeChrom displayType,
                PaneKey paneKey, List<ProteinAbundanceBindingSource.ProteinAbundanceRow> rows)
                : base(document, selectedProtein, result, displayType, paneKey, rows)
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
