/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Controls.Graphs
{
    internal class AreaPeptideGraphPane : SummaryPeptideGraphPane
    {
        public AreaPeptideGraphPane(GraphSummary graphSummary, PaneKey paneKey)
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