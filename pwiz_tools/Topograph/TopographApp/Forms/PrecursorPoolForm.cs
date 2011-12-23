using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using pwiz.Topograph.Enrichment;
using pwiz.Topograph.Model;
using ZedGraph;

namespace pwiz.Topograph.ui.Forms
{
    public partial class PrecursorPoolForm : PeptideFileAnalysisForm
    {
        public PrecursorPoolForm(PeptideFileAnalysis peptideFileAnalysis) : base(peptideFileAnalysis)
        {
            InitializeComponent();
            zedGraphControl.GraphPane.Title.IsVisible = false;
            zedGraphControl.GraphPane.YAxis.Title.Text = "Chromatogram Area";
            zedGraphControl.GraphPane.XAxis.Title.Text = "Labeled form";
        }

        protected override void PeptideFileAnalysisChanged()
        {
            base.PeptideFileAnalysisChanged();
            Recalc();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Recalc();
        }

        public void Recalc()
        {
            zedGraphControl.GraphPane.CurveList.Clear();
            zedGraphControl.GraphPane.GraphObjList.Clear();
            IDictionary<TracerFormula, double> bestMatch;
            var peaks = PeptideFileAnalysis.Peaks;
            var peaksDict = peaks.ToDictionary();
            var peaksDictList = peaksDict.ToList();
            double turnover;
            double turnoverScore;
            var precursorEnrichment = PeptideAnalysis.GetTurnoverCalculator().ComputePrecursorEnrichmentAndTurnover(peaksDict, out turnover, out turnoverScore, out bestMatch);
            dataGridViewPrecursorPool.Rows.Clear();
            if (precursorEnrichment == null)
            {
                tbxTurnover.Text = "";
                tbxTurnoverScore.Text = "";
                dataGridViewPrecursorPool.Rows.Clear();
            }
            else
            {
                tbxTurnover.Text = turnover.ToString("0.##%");
                tbxTurnoverScore.Text = turnoverScore.ToString();
                var tracerDefs = Workspace.GetTracerDefs();
                if (dataGridViewPrecursorPool.Rows.Count != tracerDefs.Count)
                {
                    dataGridViewPrecursorPool.Rows.Clear();
                    dataGridViewPrecursorPool.Rows.Add(tracerDefs.Count);
                }
                for (int i = 0; i < tracerDefs.Count; i ++)
                {
                    var tracerDef = tracerDefs[i];
                    var row = dataGridViewPrecursorPool.Rows[i];
                    row.Cells[colTracer.Index].Value = tracerDef.Name;
                    row.Cells[colPercent.Index].Value = precursorEnrichment.GetElementCount(tracerDef.Name);
                }
            }
            var observedDistribution = new PointPairList();
            var matchedDistribution = new PointPairList();
            var labels = new List<string>();
            for (int i = 0; i < peaksDictList.Count(); i++)
            {
                var tracerFormula = peaksDictList[i].Key;
                labels.Add(tracerFormula.ToDisplayString());
                observedDistribution.Add(i, peaksDictList[i].Value);
                if (bestMatch == null)
                {
                    matchedDistribution.Add(i, 1.0);
                }
                else
                {
                    matchedDistribution.Add(i, bestMatch[tracerFormula]);
                }
            }
            zedGraphControl.GraphPane.AddBar("Observed", observedDistribution, Color.Black);
            zedGraphControl.GraphPane.AddBar("Predicted", matchedDistribution, Color.Blue);
            zedGraphControl.GraphPane.XAxis.Type = AxisType.Text;
            zedGraphControl.GraphPane.XAxis.Scale.TextLabels = labels.ToArray();
            zedGraphControl.AxisChange();
        }
    }
}
