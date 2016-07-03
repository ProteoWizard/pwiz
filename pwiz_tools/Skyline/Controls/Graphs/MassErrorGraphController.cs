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
using System;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.Controls;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Controls.Graphs
{
    class MassErrorGraphController : GraphSummary.IControllerSplit
    {
        public static GraphTypeSummary GraphType
        {
            get { return Helpers.ParseEnum(Settings.Default.MassErrorGraphType, GraphTypeSummary.replicate); }
            set { Settings.Default.MassErrorGraphType = value.ToString(); }
        }

        public GraphSummary GraphSummary { get; set; }

        public IFormView FormView { get { return new GraphSummary.AreaGraphView(); } }

        public void OnActiveLibraryChanged()
        {
            if (GraphSummary.GraphPanes.OfType<MassErrorReplicateGraphPane>().Any())
                GraphSummary.UpdateUI();
        }

        public void OnResultsIndexChanged()
        {
            if (GraphSummary.GraphPanes.OfType<MassErrorReplicateGraphPane>().Any() /* || !Settings.Default.AreaAverageReplicates */ /*||
                    RTLinearRegressionGraphPane.ShowReplicate == ReplicateDisplay.single*/)
                GraphSummary.UpdateUI();
        }

        public void OnRatioIndexChanged()
        {
            if (GraphSummary.GraphPanes.OfType<MassErrorReplicateGraphPane>().Any() /* || !Settings.Default.AreaAverageReplicates */)
                GraphSummary.UpdateUI();
        }


        public void OnUpdateGraph()
        {
            GraphSummary.DoUpdateGraph(this, GraphType);
        }

        public bool IsReplicatePane(SummaryGraphPane pane)
        {
            return pane is MassErrorReplicateGraphPane;
        }

        public bool IsPeptidePane(SummaryGraphPane pane)
        {
            return pane is MassErrorPeptideGraphPane;
        }

        public SummaryGraphPane CreateReplicatePane(PaneKey key)
        {
            return new MassErrorReplicateGraphPane(GraphSummary, key);
        }

        public SummaryGraphPane CreatePeptidePane(PaneKey key)
        {
            return new MassErrorPeptideGraphPane(GraphSummary, key);
        }

        public bool HandleKeyDownEvent(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.F7:
                    if (!e.Alt && !(e.Shift && e.Control))
                    {
                        if (e.Control)
                            Settings.Default.MassErrorGraphType = GraphTypeSummary.peptide.ToString();
                        else
                            Settings.Default.MassErrorGraphType = GraphTypeSummary.replicate.ToString();
                        GraphSummary.UpdateUI();
                    }
                    break;
            }
            return false;
        }
    }
}
