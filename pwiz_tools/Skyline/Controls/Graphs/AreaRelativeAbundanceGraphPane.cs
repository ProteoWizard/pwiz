using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Controls.Graphs
{
    // TODO make private
    public class AreaRelativeAbundanceGraphPane : SummaryRelativeAbundanceGraphPane
    {
        public AreaRelativeAbundanceGraphPane(GraphSummary graphSummary, PaneKey paneKey)
            : base(graphSummary, paneKey)
        {
        }
        protected override GraphData CreateGraphData(PeptideGroupDocNode selectedProtein, SkylineDataSchema dataSchema)
        {
            return new AreaGraphData(Document, dataSchema, selectedProtein, GraphSummary.ResultsIndex, AnyMolecules);
        }

        protected override void UpdateAxes()
        {
            YAxis.Title.Text = Resources.AreaPeptideGraphPane_UpdateAxes_Peak_Area;

            base.UpdateAxes();

        }

        internal class AreaGraphData : GraphData
        {
            public AreaGraphData(SrmDocument document, SkylineDataSchema schema,
                PeptideGroupDocNode selectedProtein,
                int result, bool anyMolecules)
                : base(document, schema, selectedProtein, result, anyMolecules)
            {
            }

            public override double MaxValueSetting { get { return Settings.Default.PeakAreaMaxArea; } }
            public override double MaxCvSetting { get { return Settings.Default.PeakAreaMaxCv; } }
        }
    }

}
