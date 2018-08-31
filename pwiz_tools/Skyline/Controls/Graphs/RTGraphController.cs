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

using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.Collections;
using pwiz.Common.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.RetentionTimes;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Controls.Graphs
{
    public enum PlotTypeRT { correlation, residuals }

    public enum PointsTypeRT { targets, targets_fdr, standards, decoys }

    interface IUpdateGraphPaneController
    {
        bool UpdateUIOnIndexChanged();
        bool UpdateUIOnLibraryChanged();
    }

    public sealed class RTGraphController : GraphSummary.IController
    {
        public static PlotTypeRT PlotType
        {
            get { return Helpers.ParseEnum(Settings.Default.RTPlotType, PlotTypeRT.correlation); }
            set { Settings.Default.RTPlotType = value.ToString(); }
        }

        public static PointsTypeRT PointsType
        {
            get { return Helpers.ParseEnum(Settings.Default.RTPointsType, PointsTypeRT.targets); }
            set { Settings.Default.RTPointsType = value.ToString(); }
        }

        public static RegressionMethodRT RegressionMethod
        {
            get { return Helpers.ParseEnum(Settings.Default.RTRegressionMethod, RegressionMethodRT.linear); }
            set { Settings.Default.RTRegressionMethod = value.ToString(); }
        }

        public static GraphTypeSummary GraphType
        {
            get { return Helpers.ParseEnum(Settings.Default.RTGraphType, GraphTypeSummary.invalid); }
            set { Settings.Default.RTGraphType = value.ToString(); }
        }

        public static bool CanDoRefinementForRegressionMethod
        {
            get
            {
                switch (RegressionMethod)
                {
                    case RegressionMethodRT.kde:
                        return false;
                    case RegressionMethodRT.loess:
                        return false;
                    case RegressionMethodRT.linear:
                        return true;
                    default:
                        return true;
                } 
            }
        }

        public static double OutThreshold { get { return Settings.Default.RTResidualRThreshold; } }

        public GraphSummary GraphSummary { get; set; }

        UniqueList<GraphTypeSummary> GraphSummary.IController.GraphTypes
        {
            get { return Settings.Default.RTGraphTypes; }
            set { Settings.Default.RTGraphTypes = value; }
        }

        public IFormView FormView { get { return new GraphSummary.RTGraphView(); } }

        public bool ShowDelete(Point mousePt)
        {
            var regressionGraphPane = GraphSummary.GraphPaneFromPoint(mousePt) as RTLinearRegressionGraphPane;
            return (regressionGraphPane != null &&
                regressionGraphPane.AllowDeletePoint(new PointF(mousePt.X, mousePt.Y)));
        } 

        public void SelectPeptide(IdentityPath peptidePath)
        {
            foreach (var regressionGraphPane in GraphSummary.GraphPanes.OfType<RTLinearRegressionGraphPane>())
            {
                regressionGraphPane.SelectPeptide(peptidePath);
            }
        }

        public bool ShowDeleteOutliers
        {
            get
            {
                var regressionGraphPane = GraphSummary.GraphPanes.FirstOrDefault() as RTLinearRegressionGraphPane;
                return (regressionGraphPane != null && regressionGraphPane.HasOutliers);
            }
        }

        public PeptideDocNode[] Outliers
        {
            get
            {
                var regressionGraphPane = GraphSummary.GraphPanes.FirstOrDefault() as RTLinearRegressionGraphPane;
                return (regressionGraphPane != null ? regressionGraphPane.Outliers : null);
            }
        }

        public RetentionTimeRegression RegressionRefined
        {
            get
            {
                var regressionGraphPane = GraphSummary.GraphPanes.FirstOrDefault() as RTLinearRegressionGraphPane;
                return (regressionGraphPane != null && regressionGraphPane.IsRefined ? regressionGraphPane.RegressionRefined : null);
            }
        }

        public RetentionTimeStatistics StatisticsRefined
        {
            get
            {
                var regressionGraphPane = GraphSummary.GraphPanes.FirstOrDefault() as RTLinearRegressionGraphPane;
                return (regressionGraphPane != null ? regressionGraphPane.StatisticsRefined : null);
            }
        }

        public void OnDocumentChanged(SrmDocument oldDocument, SrmDocument newDocument)
        {

        }

        public void OnActiveLibraryChanged()
        {
            var fod = GraphSummary.GraphPanes.FirstOrDefault() as IUpdateGraphPaneController;
            if (fod != null && fod.UpdateUIOnLibraryChanged())
                GraphSummary.UpdateUI();
        }

        public void OnResultsIndexChanged()
        {
            var fod = GraphSummary.GraphPanes.FirstOrDefault() as IUpdateGraphPaneController;
            if (fod != null && fod.UpdateUIOnIndexChanged())
                GraphSummary.UpdateUI();
            
        }

        public void OnRatioIndexChanged()
        {
            // Retention times are not impacted by the ratio index
        }

        public void OnUpdateGraph()
        {
            var pane = GraphSummary.GraphPanes.FirstOrDefault();
            switch (GraphSummary.Type)
            {
                case GraphTypeSummary.score_to_run_regression:

                    if (!(pane is RTLinearRegressionGraphPane) || ((RTLinearRegressionGraphPane) pane).RunToRun)
                    {
                        GraphSummary.GraphPanes = new[] {new RTLinearRegressionGraphPane(GraphSummary, false)};
                    }
                    break;
                case GraphTypeSummary.run_to_run_regression:
                    if (!(pane is RTLinearRegressionGraphPane) || !((RTLinearRegressionGraphPane) pane).RunToRun)
                    {
                        GraphSummary.GraphPanes = new[] {new RTLinearRegressionGraphPane(GraphSummary, true)};
                    }
                    break;
                case GraphTypeSummary.replicate:
                    if (!(GraphSummary.GraphPanes.FirstOrDefault() is RTReplicateGraphPane))
                    {
                        GraphSummary.GraphPanes = new[] {new RTReplicateGraphPane(GraphSummary)};
                    }
                    break;
                case GraphTypeSummary.peptide:
                    if (!(GraphSummary.GraphPanes.FirstOrDefault() is RTPeptideGraphPane))
                    {
                        GraphSummary.GraphPanes = new[] {new RTPeptideGraphPane(GraphSummary)};
                    }
                    break;
                case GraphTypeSummary.schedule:
                    if (!(GraphSummary.GraphPanes.FirstOrDefault() is RTScheduleGraphPane))
                    {
                        GraphSummary.GraphPanes = new[] {new RTScheduleGraphPane(GraphSummary)};
                    }
                    break;
            }
        }

        public bool HandleKeyDownEvent(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
//                case Keys.D2:
//                    if (e.Alt)
//                    {
//                        GraphSummary.Hide();
//                        return true;
//                    }
//                    break;
                case Keys.F8:
                    if (!e.Alt && !(e.Shift && e.Control))
                    {
                        GraphTypeSummary type;
                        if (e.Shift)
                            type = GraphTypeSummary.score_to_run_regression;
                        else if (e.Control)
                            type = GraphTypeSummary.peptide;
                        else
                            type = GraphTypeSummary.replicate;

                        Settings.Default.RTGraphTypes.Insert(0, type);

                        Program.MainWindow.ShowGraphRetentionTime(true, type);
                        return true;
                    }
                    break;
            }
            return false;
        }

        public string Text
        {
            get { return Resources.SkylineWindow_CreateGraphRetentionTime_Retention_Times; }
        }
    }
}
