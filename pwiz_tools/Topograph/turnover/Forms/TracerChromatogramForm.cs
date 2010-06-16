using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using pwiz.MSGraph;
using pwiz.Topograph.Data;
using pwiz.Topograph.Enrichment;
using pwiz.Topograph.Model;
using pwiz.Topograph.Util;
using ZedGraph;

namespace pwiz.Topograph.ui.Forms
{
    public partial class TracerChromatogramForm : AbstractChromatogramForm
    {
        private ZedGraphControl _tracerAmountsGraph;
        public TracerChromatogramForm(PeptideFileAnalysis peptideFileAnalysis) 
            : base(peptideFileAnalysis)
        {
            InitializeComponent();
            splitContainer1.Panel2.Controls.Add(msGraphControl);
            colAmount.DefaultCellStyle.Format = "0.##%";
            colTracerPercent.DefaultCellStyle.Format = "0.##%";
            _tracerAmountsGraph = new ZedGraphControl
                                      {
                                          Dock = DockStyle.Fill,
                                      };
            splitContainer2.Panel2.Controls.Add(_tracerAmountsGraph);
        }

        protected override void Recalc()
        {
            cbxAutoFindPeak.Checked = PeptideFileAnalysis.AutoFindPeak;
            msGraphControl.GraphPane.GraphObjList.Clear();
            msGraphControl.GraphPane.CurveList.Clear();
            _tracerAmountsGraph.GraphPane.GraphObjList.Clear();
            _tracerAmountsGraph.GraphPane.CurveList.Clear();
            if (!PeptideFileAnalysis.IsMzKeySetComplete(PeptideFileAnalysis.Chromatograms.GetKeys()))
            {
                return;
            }
            IPointList scores;
            var points = GetPoints(out scores);
            var amounts = GetDistribution(points);
            var entries = points.ToArray();
            if (dataGridView1.Rows.Count != entries.Length)
            {
                dataGridView1.Rows.Clear();
                dataGridView1.Rows.Add(entries.Length);
            }
            if (cbxShowScore.Checked)
            {
                msGraphControl.GraphPane.AddCurve("Score", scores, Color.Black, SymbolType.None)
                    .IsY2Axis = true;
            }
            for (int iCandidate = 0; iCandidate < entries.Count(); iCandidate++)
            {
                var entry = entries[iCandidate];
                var label = entry.Key.ToDisplayString();
                var row = dataGridView1.Rows[iCandidate];
                row.Cells[colFormula.Index].Value = label;
                row.Cells[colAmount.Index].Value = amounts[entry.Key];
                row.Cells[colFormula.Index].Style.BackColor = row.Cells[colAmount.Index].Style.BackColor 
                    = DistributionResultsForm.GetColor(iCandidate, entries.Length);
                if (dataGridView1.SelectedRows.Count == 0 || row.Selected)
                {
                    var curve = new ChromatogramGraphItem
                    {
                        Color = DistributionResultsForm.GetColor(iCandidate, entries.Length),
                        Points = entry.Value,
                    };
                    msGraphControl.AddGraphItem(msGraphControl.GraphPane, curve);
                }
            }
            if (PeptideFileAnalysis.PeakStart.HasValue)
            {
                double selStart = PeptideFileAnalysis.TimeFromScanIndex(PeptideFileAnalysis.PeakStart.Value);
                double selEnd = PeptideFileAnalysis.TimeFromScanIndex(PeptideFileAnalysis.PeakEnd.Value);
                const double selectionBoxHeight = int.MaxValue;
                selectionBoxObj = new BoxObj(selStart, selectionBoxHeight,
                                                 selEnd - selStart,
                                                 selectionBoxHeight, Color.Goldenrod,
                                                 Color.Goldenrod)
                {
                    IsClippedToChartRect = true,
                    ZOrder = ZOrder.F_BehindGrid,
                };
                msGraphControl.GraphPane.GraphObjList.Add(selectionBoxObj);
            }
            msGraphControl.AxisChange();
            msGraphControl.Invalidate();
            double turnover;
            IDictionary<TracerFormula, double> bestMatch;
            var precursorEnrichment = PeptideAnalysis.GetTurnoverCalculator().ComputePrecursorEnrichmentAndTurnover(amounts, out turnover, out bestMatch);
            gridViewTracerPercents.Rows.Clear();
            if (precursorEnrichment == null)
            {
                tbxTurnover.Text = "";
                tbxPrecursorPool.Text = "";
            }
            else
            {
                tbxTurnover.Text = turnover.ToString("0.##%");
                tbxPrecursorPool.Text = precursorEnrichment.ToDisplayString();
                foreach (var tracerDef in Workspace.GetTracerDefs())
                {
                    var row = gridViewTracerPercents.Rows[gridViewTracerPercents.Rows.Add()];
                    row.Cells[0].Value = tracerDef.Name;
                    row.Cells[1].Value = GetTracerPercent(tracerDef, amounts) / 100;
                }
            }
            var observedDistribution = new PointPairList();
            var matchedDistribution = new PointPairList();
            var labels = new List<string>();
            for (int i = 0; i < entries.Count(); i++)
            {
                var tracerFormula = entries[i].Key;
                labels.Add(tracerFormula.ToDisplayString());
                observedDistribution.Add(i, amounts[tracerFormula]);
                matchedDistribution.Add(i, bestMatch[tracerFormula]);
            }
            _tracerAmountsGraph.GraphPane.AddBar("Observed", observedDistribution, Color.Black);
            _tracerAmountsGraph.GraphPane.AddBar("Predicted", matchedDistribution, Color.Blue);
            _tracerAmountsGraph.GraphPane.XAxis.Type = AxisType.Text;
            _tracerAmountsGraph.GraphPane.XAxis.Scale.TextLabels = labels.ToArray();
            _tracerAmountsGraph.AxisChange();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            UpdateUi();
        }

        private IDictionary<TracerFormula,IPointList> GetPoints(out IPointList scorePoints)
        {
            var pointDict = new Dictionary<TracerFormula, IList<double>>();
            var chromatograms = new Dictionary<MzKey, IList<double>>();
            bool smooth = Smooth;
            foreach (var chromatogram in PeptideFileAnalysis.Chromatograms.ListChildren())
            {
                var intensities = chromatogram.GetIntensities();
                if (smooth)
                {
                    intensities = ChromatogramData.SavitzkyGolaySmooth(intensities);
                }
                chromatograms.Add(chromatogram.MzKey, intensities);
            }
            int massCount = PeptideAnalysis.GetMassCount();
            var peptideDistributions = new PeptideDistributions(PeptideFileAnalysis);
            var times = PeptideFileAnalysis.Times.ToArray();
            var scores = new List<double>();
            var turnoverCalculator = PeptideAnalysis.GetTurnoverCalculator();
            var tracerFormulas = turnoverCalculator.ListTracerFormulas();
            var theoreticalIntensities = turnoverCalculator.GetTheoreticalIntensities(tracerFormulas);
            for (int i = 0; i < times.Length; i++)
            {
                var intensities = new List<double>();
                for (int iMass = 0; iMass < massCount; iMass++)
                {
                    double intensity = 0;
                    for (int charge = PeptideAnalysis.MinCharge; charge <= PeptideAnalysis.MaxCharge; charge++)
                    {
                        intensity += chromatograms[new MzKey(charge, iMass)][i];
                    }
                    intensities.Add(intensity);
                }
                var peptideDistribution = new PeptideDistribution(peptideDistributions, PeptideQuantity.tracer_count);
                IDictionary<TracerFormula,IList<double>> predictedIntensities;
                PeptideAnalysis.GetTurnoverCalculator().GetTracerAmounts(peptideDistribution, intensities, out predictedIntensities, tracerFormulas, theoreticalIntensities);
                foreach (var entry in predictedIntensities)
                {
                    IList<double> list;
                    if (!pointDict.TryGetValue(entry.Key, out list))
                    {
                        list = new List<double>();
                        pointDict.Add(entry.Key, list);
                    }
                    list.Add(entry.Value.Sum());
                }
                scores.Add(peptideDistribution.Score);
            }
            var points = new SortedDictionary<TracerFormula, IPointList>();
            foreach (var entry in pointDict)
            {
                points.Add(entry.Key, new PointPairList(times, entry.Value.ToArray()));
            }
            scorePoints = new PointPairList(times, scores);
            return points;
        }

        private IDictionary<TracerFormula, double> GetDistribution(IDictionary<TracerFormula, IPointList> points)
        {
            var rawResult = new Dictionary<TracerFormula, double>();
            double total = 0;
            if (!PeptideFileAnalysis.PeakStartTime.HasValue)
            {
                return rawResult;
            }
            double startTime = PeptideFileAnalysis.PeakStartTime.Value;
            double endTime = PeptideFileAnalysis.PeakEndTime.Value;
            foreach (var entry in points)
            {
                double value = 0;
                for (int i = 0; i < entry.Value.Count; i++)
                {
                    if (entry.Value[i].X < startTime || entry.Value[i].X > endTime)
                    {
                        continue;
                    }
                    value += entry.Value[i].Y;
                }
                total += value;
                rawResult.Add(entry.Key, value);
            }
            if (total == 0)
            {
                return rawResult;
            }
            return Dictionaries.Scale(rawResult, 1/total);
        }

        private void cbxAutoFindPeak_CheckedChanged(object sender, EventArgs e)
        {
            PeptideFileAnalysis.AutoFindPeak = cbxAutoFindPeak.Checked;
        }

        private void dataGridView1_SelectionChanged(object sender, EventArgs e)
        {
            UpdateUi();
        }

        private double GetTracerPercent(TracerDef tracerDef, IDictionary<TracerFormula, double> distribution)
        {
            int maxTracerCount = tracerDef.GetMaximumTracerCount(PeptideFileAnalysis.Peptide.Sequence);
            if (maxTracerCount == 0)
            {
                return 0;
            }
            double result = 0;
            foreach (var entry in distribution)
            {
                result += entry.Key.GetElementCount(tracerDef.Name) * 100.0 / maxTracerCount * entry.Value;
            }
            return result;
        }
        private void cbxShowScore_CheckedChanged(object sender, EventArgs e)
        {
            UpdateUi();
        }

    }
}
