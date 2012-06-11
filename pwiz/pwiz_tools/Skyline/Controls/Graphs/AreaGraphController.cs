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
using System;
using System.Windows.Forms;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Controls.Graphs
{
    public enum GraphTypeArea { replicate, peptide }

    public enum AreaNormalizeToView{ area_percent_view, area_maximum_view, area_ratio_view, none }

    public enum AreaScope{ document, protein }

    public sealed class AreaGraphController : GraphSummary.IController
    {
        public static GraphTypeArea GraphType
        {
            get
            {
                return (GraphTypeArea)Enum.Parse(typeof(GraphTypeArea),
                                               Settings.Default.AreaGraphType);
            }

            set
            {
                Settings.Default.AreaGraphType = value.ToString();
            }
        }

        public static AreaNormalizeToView AreaView
        {
            get
            {
                return (AreaNormalizeToView)Enum.Parse(typeof(AreaNormalizeToView),
                                                       Settings.Default.AreaNormalizeToView);
            }

            set
            {
                Settings.Default.AreaNormalizeToView = value.ToString();
            }
        }

        public static AreaScope AreaScope
        {
            get
            {
                return (AreaScope)Enum.Parse(typeof(AreaScope),
                                                       Settings.Default.PeakAreaScope);
            }

            set
            {
                Settings.Default.PeakAreaScope = value.ToString();
            }
        }

        public GraphSummary GraphSummary { get; set; }

        public void OnActiveLibraryChanged()
        {
            if (GraphSummary.GraphPane is AreaReplicateGraphPane)
                GraphSummary.UpdateUI();
        }

        public void OnResultsIndexChanged()
        {
            if (GraphSummary.GraphPane is AreaReplicateGraphPane /* || !Settings.Default.AreaAverageReplicates */ ||
                    RTLinearRegressionGraphPane.ShowReplicate == ReplicateDisplay.single)
                GraphSummary.UpdateUI();
        }

        public void OnRatioIndexChanged()
        {
            if (GraphSummary.GraphPane is AreaReplicateGraphPane /* || !Settings.Default.AreaAverageReplicates */)
                GraphSummary.UpdateUI();
        }

        public void OnUpdateGraph()
        {
            switch (GraphType)
            {
                case GraphTypeArea.replicate:
                    if (!(GraphSummary.GraphPane is AreaReplicateGraphPane))
                        GraphSummary.GraphPane = new AreaReplicateGraphPane(GraphSummary);
                    break;
                case GraphTypeArea.peptide:
                    if (!(GraphSummary.GraphPane is AreaPeptideGraphPane))
                        GraphSummary.GraphPane = new AreaPeptideGraphPane(GraphSummary);
                    break;
            }
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
                            Settings.Default.AreaGraphType = GraphTypeArea.peptide.ToString();
                        else
                            Settings.Default.AreaGraphType = GraphTypeArea.replicate.ToString();
                        GraphSummary.UpdateUI();
                    }
                    break;
            }
            return false;
        }
    }
}
