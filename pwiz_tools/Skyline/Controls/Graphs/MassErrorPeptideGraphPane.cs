/*
 * Original author: Alex MacLean <alexmaclean2000 .at. gmail.com>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2016 University of Washington - Seattle, WA
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
    public class MassErrorPeptideGraphPane : SummaryPeptideGraphPane
    {
        public MassErrorPeptideGraphPane(GraphSummary graphSummary, PaneKey paneKey)
            : base(graphSummary, paneKey)
        {
        }

        protected override GraphData CreateGraphData(SrmDocument document, PeptideGroupDocNode selectedProtein,
            TransitionGroupDocNode selectedGroup, DisplayTypeChrom displayType)
        {
            int? result = null;
            if (RTLinearRegressionGraphPane.ShowReplicate == ReplicateDisplay.single)
                result = GraphSummary.ResultsIndex;
            return new MassErrorGraphData(document, selectedGroup, selectedProtein, result, displayType, PaneKey);
        }

        protected override void UpdateAxes()
        {
            var aggregateOp = GraphValues.AggregateOp.FromCurrentSettings();
            YAxis.Title.Text = aggregateOp.Cv
                ? GraphsResources.MassErrorReplicateGraphPane_UpdateGraph_Mass_Error_No_Ppm
                : GraphsResources.MassErrorReplicateGraphPane_UpdateGraph_Mass_Error;

            base.UpdateAxes();
            
            YAxis.Scale.MinAuto = true;
            if (Settings.Default.MinMassError != 0)
                YAxis.Scale.Min = Settings.Default.MinMassError;
            if (Settings.Default.MaxMassError != 0)
                YAxis.Scale.Max = Settings.Default.MaxMassError;
            AxisChange();
        }        

        internal class MassErrorGraphData : GraphData
        {
            public MassErrorGraphData(SrmDocument document,
                                 TransitionGroupDocNode selectedGroup,
                                 PeptideGroupDocNode selectedProtein,
                                 int? result,
                                 DisplayTypeChrom displayType,
                                 PaneKey paneKey)
                : base(document, selectedGroup, selectedProtein, result, displayType, null, paneKey)
            {
            }
            
           // public override double MaxValueSetting { get { return Settings.Default.PeakAreaMaxArea; } }
           // public override double MaxCVSetting { get { return Settings.Default.PeakAreaMaxCv; } }

            protected override double? GetValue(TransitionGroupChromInfo chromInfo)
            {
                return chromInfo.MassError;
            }

            protected override double GetValue(TransitionChromInfo chromInfo)
            {
                return chromInfo.MassError ?? 0;
            }
        }
    }
}
