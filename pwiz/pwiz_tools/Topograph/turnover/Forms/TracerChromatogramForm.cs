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
using ZedGraph;

namespace pwiz.Topograph.ui.Forms
{
    public partial class TracerChromatogramForm : AbstractChromatogramForm
    {
        public TracerChromatogramForm(PeptideFileAnalysis peptideFileAnalysis) 
            : base(peptideFileAnalysis)
        {
            InitializeComponent();
            splitContainer1.Panel2.Controls.Add(msGraphControl);
        }

        protected override void Recalc()
        {
            cbxAutoFindPeak.Checked = PeptideFileAnalysis.AutoFindPeak;
            msGraphControl.GraphPane.GraphObjList.Clear();
            msGraphControl.GraphPane.CurveList.Clear();
            if (!PeptideFileAnalysis.IsMzKeySetComplete(PeptideFileAnalysis.Chromatograms.GetKeys()))
            {
                return;
            }
            var points = GetPoints();
            var entries = points.ToArray();
            for (int iCandidate = 0; iCandidate < entries.Count(); iCandidate++)
            {
                var entry = entries[iCandidate];
                var label = entry.Key.ToString();
                if (label.Length == 0)
                {
                    label = "No tracers";
                }
                var curve = new ChromatogramGraphItem
                                {
                                    Color = DistributionResultsForm.GetColor(iCandidate, entries.Length),
                                    Points = entry.Value,
                                    Title = label
                                };
                msGraphControl.AddGraphItem(msGraphControl.GraphPane, curve);
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
            msGraphControl.Invalidate();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            UpdateUi();
        }

        private IDictionary<TracerFormula,IPointList> GetPoints()
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
                PeptideAnalysis.GetTurnoverCalculator().GetTracerAmounts(peptideDistribution, intensities, out predictedIntensities);
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
            }
            var points = new SortedDictionary<TracerFormula, IPointList>();
            foreach (var entry in pointDict)
            {
                points.Add(entry.Key, new PointPairList(times, entry.Value.ToArray()));
            }
            return points;
        }

        private void cbxAutoFindPeak_CheckedChanged(object sender, EventArgs e)
        {
            PeptideFileAnalysis.AutoFindPeak = cbxAutoFindPeak.Checked;
        }
    }
}
