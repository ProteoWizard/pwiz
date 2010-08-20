using System;
using System.Windows.Forms;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Controls.Graphs
{
    public enum GraphTypeArea { replicate, peptide }

    public sealed class AreaGraphController : GraphSummary.IController
    {
        public static GraphTypeArea GraphType
        {
            get
            {
                try
                {
                    return (GraphTypeArea)Enum.Parse(typeof(GraphTypeArea),
                                                   Settings.Default.AreaGraphType);
                }
                catch (Exception)
                {
                    return GraphTypeArea.replicate;
                }
            }

            set
            {
                Settings.Default.AreaGraphType = value.ToString();
            }
        }

        public GraphSummary GraphSummary { get; set; }

        public void OnResultsIndexChanged()
        {
            if (GraphSummary.GraphPane is AreaReplicateGraphPane /* || !Settings.Default.AreaAverageReplicates */)
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
