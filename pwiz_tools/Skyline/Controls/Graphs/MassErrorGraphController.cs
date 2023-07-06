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

using System.Linq;
using System.Windows.Forms;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Controls.Graphs
{
    public enum PointsTypeMassError { targets, targets_1FDR, decoys }

    public enum TransitionMassError { all, best }

    public enum DisplayTypeMassError { precursors, products }

    public enum Histogram2DXAxis { retention_time, mass_to_charge}
   
    public class MassErrorGraphController : GraphSummary.IControllerSplit
    {
        public static GraphTypeSummary GraphType
        {
            get { return Helpers.ParseEnum(Settings.Default.MassErrorGraphType, GraphTypeSummary.invalid); }
            set { Settings.Default.MassErrorGraphType = value.ToString(); }
        }

        public static PointsTypeMassError PointsType
        {
            get { return Helpers.ParseEnum(Settings.Default.MassErrorPointsType, PointsTypeMassError.targets); }
            set { Settings.Default.MassErrorPointsType = value.ToString(); }
        }

        public static TransitionMassError HistogramTransiton
        {
            get { return Helpers.ParseEnum(Settings.Default.MassErrorHistogramTransition, TransitionMassError.best); }
            set { Settings.Default.MassErrorHistogramTransition = value.ToString(); }
        }

        public static DisplayTypeMassError HistogramDisplayType
        {
            get { return Helpers.ParseEnum(Settings.Default.MassErrorHistogramDisplayType, DisplayTypeMassError.products); }
            set { Settings.Default.MassErrorHistogramDisplayType = value.ToString(); }
        }

        public static Histogram2DXAxis Histogram2DXAxis
        {
            get { return Helpers.ParseEnum(Settings.Default.MassErrorHistogram2DXAxis, Histogram2DXAxis.retention_time); }
            set { Settings.Default.MassErrorHistogram2DXAxis = value.ToString(); }
        }

        public GraphSummary GraphSummary { get; set; }

        UniqueList<GraphTypeSummary> GraphSummary.IController.GraphTypes
        {
            get { return Settings.Default.MassErrorGraphTypes; }
            set { Settings.Default.MassErrorGraphTypes = value; }
        }

        public IFormView FormView { get { return new GraphSummary.AreaGraphView(); } }

        public void OnDocumentChanged(SrmDocument oldDocument, SrmDocument newDocument)
        {
            
        }

        public void OnActiveLibraryChanged()
        {
            if (GraphSummary.GraphPanes.OfType<MassErrorReplicateGraphPane>().Any())
                GraphSummary.UpdateUI();
        }

        public void OnResultsIndexChanged()
        {
            if (GraphSummary.GraphPanes.OfType<MassErrorReplicateGraphPane>().Any() /* || !Settings.Default.AreaAverageReplicates */ ||
                    RTLinearRegressionGraphPane.ShowReplicate == ReplicateDisplay.single)
                GraphSummary.UpdateUI();
        }

        public void OnNormalizeOptionChanged()
        {
            if (GraphSummary.GraphPanes.OfType<MassErrorReplicateGraphPane>().Any() /* || !Settings.Default.AreaAverageReplicates */)
                GraphSummary.UpdateUI();
        }


        public void OnUpdateGraph()
        {
            switch (GraphSummary.Type)
            {
                case GraphTypeSummary.replicate:
                case GraphTypeSummary.peptide:
                    GraphSummary.DoUpdateGraph(this, GraphSummary.Type);
                    break;
                case GraphTypeSummary.histogram:
                    if (!(GraphSummary.GraphPanes.FirstOrDefault() is MassErrorHistogramGraphPane))
                        GraphSummary.GraphPanes = new[] { new MassErrorHistogramGraphPane(GraphSummary) };
                    break;
                case GraphTypeSummary.histogram2d:
                    if (!(GraphSummary.GraphPanes.FirstOrDefault() is MassErrorHistogram2DGraphPane))
                        GraphSummary.GraphPanes = new[] { new MassErrorHistogram2DGraphPane(GraphSummary) };
                    break;
            }
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
                case Keys.F6:
                    if (!e.Alt && !(e.Shift && e.Control))
                    {
                        var type = e.Control ? GraphTypeSummary.peptide : GraphTypeSummary.replicate;
                        Settings.Default.MassErrorGraphTypes.Insert(0, type);

                        Program.MainWindow.ShowGraphMassError(true, type);
                        return true;
                    }
                    break;
            }
            return false;
        }

        public string Text
        {
            get { return Resources.SkylineWindow_CreateGraphMassError_Mass_Errors; }
        }
    }
}
