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
using pwiz.Common.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Controls.Graphs
{
    public enum GraphTypeRT { score_to_run_regression, replicate, schedule, peptide, run_to_run_regression }

    public enum PlotTypeRT { correlation, residuals }

    public enum PointsTypeRT { targets, standards, decoys }

    interface IUpdateGraphPaneController
    {
        bool UpdateUIOnIndexChanged();
        bool UpdateUIOnLibraryChanged();
    }

    public sealed class RTGraphController : GraphSummary.IController
    {
        public static GraphTypeRT GraphType
        {
            get { return Helpers.ParseEnum(Settings.Default.RTGraphType, GraphTypeRT.replicate); }
            set { Settings.Default.RTGraphType = value.ToString(); }
        }

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

        public static double OutThreshold { get { return Settings.Default.RTResidualRThreshold; } }

        public GraphSummary GraphSummary { get; set; }

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
            switch (GraphType)
            {
                case GraphTypeRT.score_to_run_regression:

                    if (!(pane is RTLinearRegressionGraphPane) || ((RTLinearRegressionGraphPane) pane).RunToRun)
                    {
                        GraphSummary.GraphPanes = new[] {new RTLinearRegressionGraphPane(GraphSummary, false)};
                    }
                    break;
                case GraphTypeRT.run_to_run_regression:
                    if (!(pane is RTLinearRegressionGraphPane) || !((RTLinearRegressionGraphPane) pane).RunToRun)
                    {
                        GraphSummary.GraphPanes = new[] {new RTLinearRegressionGraphPane(GraphSummary, true)};
                    }
                    break;
                case GraphTypeRT.replicate:
                    if (!(GraphSummary.GraphPanes.FirstOrDefault() is RTReplicateGraphPane))
                    {
                        GraphSummary.GraphPanes = new[] {new RTReplicateGraphPane(GraphSummary)};
                    }
                    break;
                case GraphTypeRT.peptide:
                    if (!(GraphSummary.GraphPanes.FirstOrDefault() is RTPeptideGraphPane))
                    {
                        GraphSummary.GraphPanes = new[] {new RTPeptideGraphPane(GraphSummary)};
                    }
                    break;
                case GraphTypeRT.schedule:
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
                        if (e.Shift)
                            GraphType = GraphTypeRT.score_to_run_regression;
                        else if (e.Control)
                            GraphType = GraphTypeRT.peptide;
                        else
                            GraphType = GraphTypeRT.replicate;
                        GraphSummary.UpdateUI();
                        return true;
                    }
                    break;
            }
            return false;
        }

        public bool IsRunToRun()
        {
            return GraphType == GraphTypeRT.run_to_run_regression;
        }
    }
}
