/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2010 University of Washington - Seattle, WA
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

using System.Linq;
using System.Windows.Forms;
using pwiz.Common.Controls;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Controls.Graphs
{
    public enum AreaNormalizeToView{ area_percent_view, area_maximum_view, area_ratio_view, area_global_standard_view, none }

    public enum AreaScope{ document, protein }

    public sealed class AreaGraphController : GraphSummary.IControllerSplit
    {
        public static GraphTypeSummary GraphType
        {
            get { return Helpers.ParseEnum(Settings.Default.AreaGraphType, GraphTypeSummary.replicate); }
            set { Settings.Default.AreaGraphType = value.ToString(); }
        }

        public static AreaNormalizeToView AreaView
        {
            get { return Helpers.ParseEnum(Settings.Default.AreaNormalizeToView, AreaNormalizeToView.none); }
            set { Settings.Default.AreaNormalizeToView = value.ToString(); }
        }

        public static AreaScope AreaScope
        {
            get { return Helpers.ParseEnum(Settings.Default.PeakAreaScope, AreaScope.document); }
            set { Settings.Default.PeakAreaScope = value.ToString(); }
        }

        public GraphSummary GraphSummary { get; set; }

        public IFormView FormView { get { return new GraphSummary.AreaGraphView(); } }

        public void OnActiveLibraryChanged()
        {
            if (GraphSummary.GraphPanes.OfType<AreaReplicateGraphPane>().Any())
                GraphSummary.UpdateUI();
        }

        public void OnResultsIndexChanged()
        {
            if (GraphSummary.GraphPanes.OfType<AreaReplicateGraphPane>().Any() /* || !Settings.Default.AreaAverageReplicates */ ||
                    RTLinearRegressionGraphPane.ShowReplicate == ReplicateDisplay.single)
                GraphSummary.UpdateUI();
        }

        public void OnRatioIndexChanged()
        {
            if (GraphSummary.GraphPanes.OfType<AreaReplicateGraphPane>().Any() /* || !Settings.Default.AreaAverageReplicates */)
                GraphSummary.UpdateUI();
        }

        public void OnUpdateGraph()
        {
            GraphSummary.DoUpdateGraph(this, GraphType);
        }

        public bool IsReplicatePane(SummaryGraphPane pane)
        {
            return pane is AreaReplicateGraphPane;
        }

        public bool IsPeptidePane(SummaryGraphPane pane)
        {
            return pane is AreaPeptideGraphPane;
        }

        public SummaryGraphPane CreateReplicatePane(PaneKey key)
        {
            return new AreaReplicateGraphPane(GraphSummary, key);
        }

        public SummaryGraphPane CreatePeptidePane(PaneKey key)
        {
            return new AreaPeptideGraphPane(GraphSummary, key);
        }

        public bool HandleKeyDownEvent(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
//                case Keys.D3:
//                    if (e.Alt)
//                        GraphSummary.Hide();
//                    break;
                case Keys.F7:
                    if (!e.Alt && !(e.Shift && e.Control))
                    {
                        if (e.Control)
                            Settings.Default.AreaGraphType = GraphTypeSummary.peptide.ToString();
                        else
                            Settings.Default.AreaGraphType = GraphTypeSummary.replicate.ToString();
                        GraphSummary.UpdateUI();
                    }
                    break;
            }
            return false;
        }

        public bool IsRunToRun()
        {
            return false;
        }
    }
}
