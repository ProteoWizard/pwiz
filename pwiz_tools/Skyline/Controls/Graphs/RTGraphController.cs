using System;
using System.Drawing;
using System.Windows.Forms;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Controls.Graphs
{
    public enum GraphTypeRT { regression, replicate, schedule, peptide }

    public sealed class RTGraphController : GraphSummary.IController
    {
        public static GraphTypeRT GraphType
        {
            get
            {
                try
                {
                    return (GraphTypeRT)Enum.Parse(typeof(GraphTypeRT),
                                                   Settings.Default.RTGraphType);
                }
                catch (Exception)
                {
                    return GraphTypeRT.regression;
                }
            }

            set
            {
                Settings.Default.RTGraphType = value.ToString();
            }
        }

        public static double OutThreshold { get { return Settings.Default.RTResidualRThreshold; } }

        public GraphSummary GraphSummary { get; set; }

        public bool ShowDelete(Point mousePt)
        {
            var regressionGraphPane = GraphSummary.GraphPane as RTLinearRegressionGraphPane;
            return (regressionGraphPane != null &&
                regressionGraphPane.AllowDeletePoint(new PointF(mousePt.X, mousePt.Y)));
        } 

        public void SelectPeptide(IdentityPath peptidePath)
        {
            var regressionGraphPane = GraphSummary.GraphPane as RTLinearRegressionGraphPane;
            if (regressionGraphPane != null)
                regressionGraphPane.SelectPeptide(peptidePath);
        }

        public bool ShowDeleteOutliers
        {
            get
            {
                var regressionGraphPane = GraphSummary.GraphPane as RTLinearRegressionGraphPane;
                return (regressionGraphPane != null && regressionGraphPane.HasOutliers);
            }
        }

        public PeptideDocNode[] Outliers
        {
            get
            {
                var regressionGraphPane = GraphSummary.GraphPane as RTLinearRegressionGraphPane;
                return (regressionGraphPane != null ? regressionGraphPane.Outliers : null);
            }
        }

        public RetentionTimeRegression RegressionRefined
        {
            get
            {
                var regressionGraphPane = GraphSummary.GraphPane as RTLinearRegressionGraphPane;
                return (regressionGraphPane != null ? regressionGraphPane.RegressionRefined : null);
            }
        }

        public void OnActiveLibraryChanged()
        {
            if (GraphSummary.GraphPane is RTReplicateGraphPane ||
                    RTLinearRegressionGraphPane.ShowReplicate == ReplicateDisplay.single)
                GraphSummary.UpdateUI();
        }

        public void OnResultsIndexChanged()
        {
            if (GraphSummary.GraphPane is RTReplicateGraphPane ||
                    RTLinearRegressionGraphPane.ShowReplicate == ReplicateDisplay.single)
                GraphSummary.UpdateUI();
        }

        public void OnRatioIndexChanged()
        {
            // Retention times are not impacted by the ratio index
        }

        public void OnUpdateGraph()
        {
            switch (GraphType)
            {
                case GraphTypeRT.regression:
                    if (!(GraphSummary.GraphPane is RTLinearRegressionGraphPane))
                        GraphSummary.GraphPane = new RTLinearRegressionGraphPane(GraphSummary);
                    break;
                case GraphTypeRT.replicate:
                    if (!(GraphSummary.GraphPane is RTReplicateGraphPane))
                        GraphSummary.GraphPane = new RTReplicateGraphPane(GraphSummary);
                    break;
                case GraphTypeRT.peptide:
                    if (!(GraphSummary.GraphPane is RTPeptideGraphPane))
                        GraphSummary.GraphPane = new RTPeptideGraphPane(GraphSummary);
                    break;
                case GraphTypeRT.schedule:
                    if (!(GraphSummary.GraphPane is RTScheduleGraphPane))
                        GraphSummary.GraphPane = new RTScheduleGraphPane(GraphSummary);
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
                            GraphType = GraphTypeRT.regression;
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
    }
}
