using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using ZedGraph;

namespace pwiz.Skyline.Controls.Graphs
{
    internal class AreaIntensityGraphPane : SummaryIntensityGraphPane
    {
        public AreaIntensityGraphPane(GraphSummary graphSummary, PaneKey paneKey)
            : base(graphSummary, paneKey)
        {
        }
        protected override GraphData CreateGraphData(SrmDocument document, PeptideGroupDocNode selectedProtein, TransitionGroupDocNode selectedGroup, DisplayTypeChrom displayType)
        {
            int? result = null;
            if (RTLinearRegressionGraphPane.ShowReplicate == ReplicateDisplay.single)
                result = GraphSummary.ResultsIndex;
            return new AreaGraphData(document, selectedGroup, selectedProtein, result, displayType, PaneKey);
        }
        internal class AreaGraphData : GraphData
        {
            public AreaGraphData(SrmDocument document,
                TransitionGroupDocNode selectedGroup,
                PeptideGroupDocNode selectedProtein,
                int? result,
                DisplayTypeChrom displayType,
                PaneKey paneKey)
                : base(document, selectedGroup, selectedProtein, result, displayType, null, paneKey)
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
