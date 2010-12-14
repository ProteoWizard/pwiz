using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using pwiz.Crawdad;
using pwiz.Topograph.Data;
using pwiz.Topograph.Enrichment;
using pwiz.Topograph.Model;
using pwiz.Topograph.Util;
using ZedGraph;

namespace pwiz.Topograph.ui.Forms
{
    public partial class RelexForm : PeptideFileAnalysisForm
    {
        private bool _inRecalc;
        public RelexForm(PeptideFileAnalysis peptideFileAnalysis) : base(peptideFileAnalysis)
        {
            InitializeComponent();
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

        protected void Recalc()
        {
            if (_inRecalc)
            {
                return;
            }
            try
            {
                _inRecalc = true;
                DoRecalc();
            }
            finally
            {
                _inRecalc = false;
            }
        }

        private void DoRecalc()
        {
            var otherTracerFormula = comboSecondary.SelectedItem as TracerFormula;
            comboPrimary.Items.Clear();
            comboSecondary.Items.Clear();
            msGraphControl.GraphPane.GraphObjList.Clear();
            msGraphControl.GraphPane.CurveList.Clear();
            barGraphControl.GraphPane.GraphObjList.Clear();
            barGraphControl.GraphPane.CurveList.Clear();

            var peaks = PeptideFileAnalysis.Peaks;
            if (peaks.ChildCount <= 1 || peaks.BasePeakKey == null)
            {
                return;
            }
            var tracerFormulae = new List<TracerFormula>(peaks.ListChildren().Select(p => TracerFormula.Parse(p.Name)));
            tracerFormulae.Sort();
            var baseTracerFormula = TracerFormula.Parse(peaks.BasePeakKey);
            foreach (var tracerFormula in tracerFormulae)
            {
                comboPrimary.Items.Add(tracerFormula);
                if (Equals(tracerFormula, baseTracerFormula))
                {
                    comboPrimary.SelectedIndex = comboPrimary.Items.Count - 1;
                }
                if (Equals(tracerFormula, baseTracerFormula))
                {
                    continue;
                }
                comboSecondary.Items.Add(tracerFormula);
                if (Equals(tracerFormula, otherTracerFormula))
                {
                    comboSecondary.SelectedIndex = comboSecondary.Items.Count - 1;
                }
            }
            if (comboSecondary.SelectedIndex < 0)
            {
                comboSecondary.SelectedIndex = 0;
            }
            otherTracerFormula = comboSecondary.SelectedItem as TracerFormula;
            var basePeak = peaks.GetChild(baseTracerFormula.ToString());
            if (basePeak == null)
            {
                tbxStart.Text = "";
                tbxEnd.Text = "";
            }
            else
            {
                tbxStart.Text = basePeak.StartTime.ToString();
                tbxEnd.Text = basePeak.EndTime.ToString();
            }

            var otherPeak = peaks.GetPeak(otherTracerFormula);
            if (otherPeak == null)
            {
                tbxOtherStart.Text = "";
            }
            else
            {
                tbxOtherStart.Text = otherPeak.StartTime.ToString();
                tbxOtherEnd.Text = otherPeak.EndTime.ToString();
                tbxCorrelation.Text = otherPeak.Correlation.ToString();
                tbxSlope.Text = otherPeak.RatioToBase.ToString();
                tbxSlopeError.Text = otherPeak.RatioToBaseError.ToString();
                tbxIntercept.Text = otherPeak.Intercept.ToString();
                tbxWidthRatio.Text = ((otherPeak.EndTime - otherPeak.StartTime)/(basePeak.EndTime - basePeak.StartTime)).ToString();
            }
            if (basePeak == null || otherPeak == null)
            {
                return;
            }
            var baseTimes = peaks.GetBaseTimes();
            var basePoints = peaks.GetValues(basePeak);
            var secondaryPoints = peaks.GetValues(otherPeak);
            var curve = msGraphControl.GraphPane.AddCurve(null, basePoints, secondaryPoints, Color.Black);
            curve.Line.IsVisible = false;
            curve.Symbol.Border.IsVisible = false;
            double maxX = basePoints.Max();
            msGraphControl.GraphPane.GraphObjList.Add(new LineObj(Color.Black, 0, otherPeak.Intercept, maxX, maxX*otherPeak.RatioToBase));
            // Fill the symbol interior with color
            curve.Symbol.Fill = new Fill(Color.Firebrick);

            barGraphControl.GraphPane.AddCurve(basePeak.TracerFormula.ToDisplayString(), baseTimes, basePoints,
                                             Color.Black);
            var scaledPoints = secondaryPoints.Select(p => p/otherPeak.RatioToBase);
            barGraphControl.GraphPane.AddCurve(otherPeak.TracerFormula.ToDisplayString(), baseTimes, scaledPoints.ToArray(),
                                             Color.Blue);
            barGraphControl.GraphPane.XAxis.Title.Text = "Time";
            barGraphControl.GraphPane.YAxis.Title.Text = "Scaled Intensity";
            barGraphControl.AxisChange();
            barGraphControl.Invalidate();
            msGraphControl.GraphPane.XAxis.Title.Text = baseTracerFormula.ToDisplayString();
            msGraphControl.GraphPane.YAxis.Title.Text = otherTracerFormula.ToDisplayString();
            msGraphControl.AxisChange();
            msGraphControl.Invalidate();
        }

        private DbPeak GetBasePeak()
        {
            return PeptideFileAnalysis.Peaks.GetBasePeak();
        }

        private DbPeak GetOtherPeak()
        {
            return PeptideFileAnalysis.Peaks.GetPeak(comboSecondary.SelectedItem as TracerFormula);
        }

        private void comboPrimary_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_inRecalc)
            {
                return;
            }
            var newBasePeak = comboPrimary.SelectedItem as TracerFormula;
            if (newBasePeak != null)
            {
                PeptideFileAnalysis.SetDistributions(PeptideFileAnalysis.Peaks.ChangeBasePeak(newBasePeak));
            }
        }

        public class BasePeak
        {
            public BasePeak(CrawdadPeak crawdadPeak) 
            {
                CrawdadPeak = crawdadPeak;
            }
            public CrawdadPeak CrawdadPeak { get; private set;}
            public override string ToString()
            {
                return "[" + CrawdadPeak.StartIndex + "-" + CrawdadPeak.EndIndex + "]";
            }

            public int Width { get { return CrawdadPeak.EndIndex - CrawdadPeak.StartIndex + 1; } }
        }

        private void comboSecondary_SelectedIndexChanged(object sender, EventArgs e)
        {
            Recalc();
        }

        private double[] GetValues(IList<double> points, int start, int end)
        {
            var result = new double[end - start + 1];
            for (int i = 0; i < result.Length; i ++)
            {
                result[i] = points[i + start];
            }
            return result;
        }

        private void MoveTime(DbPeak peak, int deltaStart, int deltaEnd)
        {
            if (peak == null)
            {
                return;
            }
            var chromatograms = PeptideFileAnalysis.Chromatograms;
            var newStartIndex = chromatograms.IndexFromTime(peak.StartTime) + deltaStart;
            var newEndIndex = chromatograms.IndexFromTime(peak.EndTime) + deltaEnd;
            newEndIndex = Math.Min(newEndIndex, chromatograms.Times.Count - 1);
            newStartIndex = Math.Min(newStartIndex, newEndIndex);
            newStartIndex = Math.Max(0, newStartIndex);
            newEndIndex = Math.Max(newStartIndex, newEndIndex);
            var newPeaks = PeptideFileAnalysis.Peaks.ChangeTime(peak.TracerFormula, chromatograms.Times[newStartIndex],
                                                                chromatograms.Times[newEndIndex]);
            PeptideFileAnalysis.SetDistributions(newPeaks);
        }

        private void btnStartLeft_Click(object sender, EventArgs e)
        {
            MoveTime(PeptideFileAnalysis.Peaks.GetBasePeak(), -1, 0);
        }

        private void btnStartRight_Click(object sender, EventArgs e)
        {
            MoveTime(PeptideFileAnalysis.Peaks.GetBasePeak(), 1, 0);
        }

        private void btnEndRight_Click(object sender, EventArgs e)
        {
            MoveTime(PeptideFileAnalysis.Peaks.GetBasePeak(), 0, 1);
        }

        private void btnEndLeft_Click(object sender, EventArgs e)
        {
            MoveTime(PeptideFileAnalysis.Peaks.GetBasePeak(), 0, -1);
        }

        private void btnOtherStartLeft_Click(object sender, EventArgs e)
        {
            MoveTime(GetOtherPeak(), -1, 0);
        }

        private void btnOtherStartRight_Click(object sender, EventArgs e)
        {
            MoveTime(GetOtherPeak(), 1, 0);
        }

        private void btnOtherEndLeft_Click(object sender, EventArgs e)
        {
            MoveTime(GetOtherPeak(), 0, -1);
        }

        private void btnOtherEndRight_Click(object sender, EventArgs e)
        {
            MoveTime(GetOtherPeak(), 0, 1);
        }

        private void tbxStart_Leave(object sender, EventArgs e)
        {

        }
    }
}
