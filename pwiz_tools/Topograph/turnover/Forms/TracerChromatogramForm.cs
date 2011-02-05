using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
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
        private IDictionary<TracerFormula, PeakDisplay> _peakLines;
        private PeakResize _peakResize;

        public TracerChromatogramForm(PeptideFileAnalysis peptideFileAnalysis) 
            : base(peptideFileAnalysis)
        {
            InitializeComponent();
            splitContainer1.Panel2.Controls.Add(msGraphControl);
            colAreaPct.DefaultCellStyle.Format = "0.##%";
            colSlopePct.DefaultCellStyle.Format = "0.##%";
            colTracerPercent.DefaultCellStyle.Format = "0.##%";
            comboAdjustPeaks.SelectedIndex = 0;
        }

        protected override bool msGraphControl_MouseDownEvent(ZedGraphControl sender, MouseEventArgs e)
        {
            _peakResize = null;
            if (base.msGraphControl_MouseDownEvent(sender, e))
            {
                return true;
            }
            if ((_peakResize = PeakResizeFromPoint(e.Location)) != null)
            {
                sender.Cursor = _peakResize.GetCursor();
                return true;
            }
            return false;
        }

        protected override bool msGraphControl_DoubleClickEvent(ZedGraphControl sender, MouseEventArgs e)
        {
//            var peakResize = PeakResizeFromPoint(e.Location);
//            if (peakResize != null)
//            {
//                var peaks = PeptideFileAnalysis.Peaks;
//                var newPeaks = peaks.AutoSizePeak(peakResize.Peak.TracerFormula,
//                                                  peakResize.IsStartIndex
//                                                      ? peakResize.Peak.StartTime
//                                                      : peakResize.Peak.EndTime);
//                if (newPeaks != null)
//                {
//                    PeptideFileAnalysis.SetDistributions(newPeaks);
//                }
//                return true;
//            }
            return base.msGraphControl_DoubleClickEvent(sender, e);
        }

        protected override bool msGraphControl_MouseUpEvent(ZedGraphControl sender, MouseEventArgs e)
        {
            if (_peakResize != null)
            {
                var point = new PointF(e.X - _peakResize.MousePt.X + _peakResize.CoordPt.X, e.Y);
                double x, y;
                msGraphControl.GraphPane.ReverseTransform(point, out x, out y);
                var peaks = PeptideFileAnalysis.Peaks;
                double newStart, newEnd;
                switch (_peakResize.LineSegment)
                {
                    case LineSegment.Start:
                        newStart = x;
                        newEnd = _peakResize.Peak.EndTime;
                        break;
                    case LineSegment.End:
                        newStart = _peakResize.Peak.StartTime;
                        newEnd = x;
                        break;
                    default:
                    case LineSegment.Middle:
                        newStart = x;
                        newEnd = x - _peakResize.Peak.StartTime + _peakResize.Peak.EndTime;
                        break;
                }
                var newPeaks = peaks.ChangeTime(_peakResize.Peak.TracerFormula, Math.Min(newStart, newEnd), Math.Max(newStart, newEnd));
                _peakResize = null;
                PeptideFileAnalysis.SetDistributions(newPeaks);
            }
            return base.msGraphControl_MouseUpEvent(sender, e);
        }

        protected override bool msGraphControl_MouseMoveEvent(ZedGraphControl sender, MouseEventArgs e)
        {
            if (_peakResize != null)
            {
                var peakDisplay = _peakResize.PeakDisplay;
                PointPair[] points = new[]
                                         {
                                             new PointPair(peakDisplay.Start, peakDisplay.Height), 
                                             new PointPair(peakDisplay.End, peakDisplay.Height), 
                                         };
                var screenPt = new PointF(e.Location.X - _peakResize.MousePt.X + _peakResize.CoordPt.X, _peakResize.CoordPt.Y);
                double x, y;
                msGraphControl.GraphPane.ReverseTransform(screenPt, out x, out y);
                switch (_peakResize.LineSegment)
                {
                    case LineSegment.Start:
                        points[0] = new PointPair(x, points[0].Y);
                        break;
                    case LineSegment.End:
                        points[1] = new PointPair(x, points[0].Y);
                        break;
                    case LineSegment.Middle:
                        points = new[]
                                     {
                                         new PointPair(x, points[0].Y),
                                         new PointPair(x - points[0].X + points[1].X, points[1].Y)
                                     };
                        break;
                }
                
                if (peakDisplay.HorizontalLine != null)
                {
                    peakDisplay.HorizontalLine.RemovePoint(1);
                    peakDisplay.HorizontalLine.RemovePoint(0);
                    peakDisplay.HorizontalLine.AddPoint(points[0]);
                    peakDisplay.HorizontalLine.AddPoint(points[1]);
                }
                foreach (var lineItemPt in new[] { 
                    new KeyValuePair<LineItem, PointPair>(peakDisplay.StartVerticalLine, points[0]),
                    new KeyValuePair<LineItem, PointPair>(peakDisplay.EndVerticalLine, points[1])
                })
                {
                    if (lineItemPt.Key == null)
                    {
                        continue;
                    }
                    lineItemPt.Key.RemovePoint(1);
                    lineItemPt.Key.RemovePoint(0);
                    lineItemPt.Key.AddPoint(new PointPair(lineItemPt.Value.X, peakDisplay.Height));
                    lineItemPt.Key.AddPoint(new PointPair(lineItemPt.Value.X, 0));
                }
                msGraphControl.Invalidate();
                sender.Cursor = _peakResize.GetCursor();
                return true;
            }
            if (base.msGraphControl_MouseMoveEvent(sender, e))
            {
                return true;
            }
            PeakResize peakResize = PeakResizeFromPoint(e.Location);
            if (peakResize != null)
            {
                sender.Cursor = peakResize.GetCursor();
                return true;
            }
            return false;
        }

        private PeakResize PeakResizeFromPoint(PointF pointF)
        {
            if (_peakLines == null)
            {
                return null;
            }
            var peaks = PeptideFileAnalysis.Peaks;
            foreach (var entry in _peakLines)
            {
                var peak = peaks.GetPeak(entry.Key);
                if (peak == null)
                {
                    continue;
                }
                var peakDisplay = entry.Value;
                var peakResize = new PeakResize
                {
                    Peak = peak,
                    MousePt = pointF,
                    PeakDisplay = peakDisplay,
                };
                if (peakDisplay.HorizontalLine != null)
                {
                    PointF startPt = msGraphControl.GraphPane.GeneralTransform(peakDisplay.HorizontalLine.Points[0], CoordType.AxisXYScale);
                    PointF endPt = msGraphControl.GraphPane.GeneralTransform(peakDisplay.HorizontalLine.Points[1],
                                                                             CoordType.AxisXYScale);
                    if (Math.Abs(pointF.Y - startPt.Y) <= 2)
                    {
                        if (Math.Abs(pointF.X - startPt.X) <= 2)
                        {
                            peakResize.LineSegment = LineSegment.Start;
                            peakResize.CoordPt = startPt;
                            return peakResize;
                        }
                        if (Math.Abs(pointF.X - endPt.X) <= 2)
                        {
                            peakResize.LineSegment = LineSegment.End;
                            peakResize.CoordPt = endPt;
                            return peakResize;
                        }
                        if (pointF.X > startPt.X && pointF.X < endPt.X)
                        {
                            peakResize.LineSegment = LineSegment.Middle;
                            peakResize.CoordPt = startPt;
                            return peakResize;
                        }
                    }
                }
                foreach (var lineItemSeg in new[] { new KeyValuePair<LineItem, LineSegment>(peakDisplay.StartVerticalLine, LineSegment.Start),
                    new KeyValuePair<LineItem, LineSegment>(peakDisplay.EndVerticalLine, LineSegment.End)})
                {
                    if (lineItemSeg.Key == null)
                    {
                        continue;
                    }
                    PointF firstPt = msGraphControl.GraphPane.GeneralTransform(lineItemSeg.Key.Points[0],
                                                                               CoordType.AxisXYScale);
                    PointF lastPt = msGraphControl.GraphPane.GeneralTransform(lineItemSeg.Key.Points[1],
                                                                              CoordType.AxisXYScale);
                    if (Math.Abs(pointF.X - firstPt.X) <= 1
                        && pointF.Y >= Math.Min(firstPt.Y, lastPt.Y)
                        && pointF.Y <= Math.Max(firstPt.Y, lastPt.Y))
                    {
                        peakResize.LineSegment = lineItemSeg.Value;
                        peakResize.CoordPt = new PointF(firstPt.X, pointF.Y);
                        return peakResize;
                    }
                }
                
            }
            return null;
        }

        private bool IsDisplayed(TracerFormula tracerFormula)
        {
            if (dataGridView1.SelectedRows.Count == 0)
            {
                return true;
            }
            for (int i = 0; i < dataGridView1.Rows.Count; i++)
            {
                var row = dataGridView1.Rows[i];
                if (row.Selected && tracerFormula.Equals(row.Tag))
                {
                    return true;
                }
            }
            return false;
        }

        protected override void Recalc()
        {
            cbxAutoFindPeak.Checked = PeptideFileAnalysis.AutoFindPeak;
            msGraphControl.GraphPane.GraphObjList.Clear();
            msGraphControl.GraphPane.CurveList.Clear();
            _peakLines = null;
            if (!PeptideFileAnalysis.IsMzKeySetComplete(PeptideFileAnalysis.Chromatograms.GetKeys()))
            {
                return;
            }
            var peakLines = new Dictionary<TracerFormula, PeakDisplay>();
            var tracerChromatograms = GetPoints();
            var peaks = PeptideFileAnalysis.Peaks;
            var entries = tracerChromatograms.Points.ToArray();
            if (dataGridView1.Rows.Count != entries.Length)
            {
                dataGridView1.Rows.Clear();
                dataGridView1.Rows.Add(entries.Length);
            }
            if (cbxShowScore.Checked)
            {
                msGraphControl.GraphPane.AddCurve("Score", tracerChromatograms.Times.ToArray(), tracerChromatograms.Scores.ToArray(), Color.Black, SymbolType.None)
                    .IsY2Axis = true;
            }
            double totalAmount = peaks.ListChildren().Sum(p => p.Area);
            double totalSlope = peaks.ListChildren().Sum(p => p.RatioToBase);
            for (int iCandidate = 0; iCandidate < entries.Count(); iCandidate++)
            {
                var entry = entries[iCandidate];
                var label = entry.Key.ToDisplayString();
                var row = dataGridView1.Rows[iCandidate];
                row.Cells[colFormula.Index].Value = label;
                double amount;
                var peak = peaks.GetPeak(entry.Key);
                if (peak != null)
                {
                    row.Cells[colAreaPct.Index].Value = peak.Area/totalAmount;
                    row.Cells[colSlopePct.Index].Value = peak.RatioToBase/totalSlope;
                    row.Cells[colStartTime.Index].Value = peak.StartTime;
                    row.Cells[colEndTime.Index].Value = peak.EndTime;
                    row.Cells[colArea.Index].Value = peak.Area;
                    row.Cells[colCorr.Index].Value = peak.Correlation;
                }
                else
                {
                    row.Cells[colAreaPct.Index].Value = null;
                    row.Cells[colStartTime.Index].Value = null;
                    row.Cells[colEndTime.Index].Value = null;
                    row.Cells[colArea.Index].Value = null;
                    row.Cells[colCorr.Index].Value = null;
                }
                row.Cells[colFormula.Index].Style.BackColor = row.Cells[colAreaPct.Index].Style.BackColor 
                    = GetColor(iCandidate, entries.Length);
                row.Tag = entry.Key;
                if (IsDisplayed(entry.Key))
                {
                    var curve = new ChromatogramGraphItem
                    {
                        Color = GetColor(iCandidate, entries.Length),
                        Points = new PointPairList(tracerChromatograms.Times, entry.Value),
                    };
                    msGraphControl.AddGraphItem(msGraphControl.GraphPane, curve);
                    if (peak != null)
                    {
                        var peakDisplay = new PeakDisplay();
                        var color = GetColor(iCandidate, entries.Length);
                        var max = MaxInRange(entry.Value, tracerChromatograms.Chromatograms.IndexFromTime(peak.StartTime), 
                            tracerChromatograms.Chromatograms.IndexFromTime(peak.EndTime));
                        peakDisplay.Start = peak.StartTime;
                        peakDisplay.End = peak.EndTime;
                        peakDisplay.Height = max;
                        if (PeaksAsHorizontalLines)
                        {
                            peakDisplay.HorizontalLine = msGraphControl.GraphPane.AddCurve(null, new[] { peakDisplay.Start, peakDisplay.End }, new[] { max, max }, color);
                        }
                        if (PeaksAsVerticalLines)
                        {
                            peakDisplay.StartVerticalLine = msGraphControl.GraphPane.AddCurve(
                                null,
                                new[] { peakDisplay.Start, peakDisplay.Start },
                                new[] {max, 0}, color, SymbolType.None
                            );
                            peakDisplay.EndVerticalLine = msGraphControl.GraphPane.AddCurve(
                                null,
                                new[] { peakDisplay.End, peakDisplay.End }, 
                                new[] { max, 0 }, color, SymbolType.None
                            );
                        }
                        peakLines.Add(entry.Key, peakDisplay);
                    }
                }
            }
//            foreach (var peakSet in tracerChromatograms.GetNonOverlappingPeakSets())
//            {
//                double start = PeptideFileAnalysis.TimeFromScanIndex(peakSet.StartIndex);
//                double end = PeptideFileAnalysis.TimeFromScanIndex(peakSet.EndIndex);
//                double rawScore = tracerChromatograms.GetScore(peakSet.StartIndex, peakSet.EndIndex);
//                double colorScore = Math.Pow(rawScore, 4);
//                var color = Color.FromArgb((int) (255 - (255*colorScore)), (int) (255*colorScore), 0);
//                var box = new BoxObj(start, int.MaxValue, end - start, int.MaxValue, Color.Black, color)
//                              {
//                                  IsClippedToChartRect = true,
//                                  ZOrder = ZOrder.F_BehindGrid,
//                              };
//                msGraphControl.GraphPane.GraphObjList.Add(box);
//                double retentionTime = tracerChromatograms.Times[peakSet.BestRetentionTime];
//                double intensity = tracerChromatograms.GetMaxIntensity(peakSet);
//                var textObj = new TextObj(retentionTime.ToString("0.##"), retentionTime,
//                                          intensity, CoordType.AxisXYScale, AlignH.Center, AlignV.Bottom)
//                                  {
//                                      FontSpec = {Fill = new Fill(color),}, 
//                                      Tag = peakSet,
//                                  };
//                msGraphControl.GraphPane.GraphObjList.Add(textObj);
//            }
            if (PeptideFileAnalysis.FirstDetectedScan.HasValue)
            {
                double time = TimeFromScanIndex(PeptideFileAnalysis.FirstDetectedScan.Value);
                msGraphControl.GraphPane.GraphObjList.Add(new LineObj(Color.Black, time, 0, time,
                                                                      1)
                                                              {
                                                                  Line = {Style = DashStyle.DashDot},
                                                                  ZOrder = ZOrder.E_BehindCurves,
                                                                  IsClippedToChartRect = true,
                                                                  Location = {CoordinateFrame = CoordType.XScaleYChartFraction}

                });
                if (PeptideFileAnalysis.LastDetectedScan != PeptideFileAnalysis.FirstDetectedScan)
                {
                    time = TimeFromScanIndex(PeptideFileAnalysis.LastDetectedScan.Value);
                    msGraphControl.GraphPane.GraphObjList.Add(new LineObj(Color.Black, time, 0, time, 1)
                                                                  {
                                                                      Line = {Style = DashStyle.DashDot},
                                                                      ZOrder = ZOrder.E_BehindCurves,
                                                                      IsClippedToChartRect = true,
                                                                      Location = { CoordinateFrame = CoordType.XScaleYChartFraction }
                                                                  });
                }
            }

            msGraphControl.AxisChange();
            msGraphControl.Invalidate();
            _peakLines = peakLines;
            tbxTracerPercentByAreas.Text = peaks.CalcTracerPercentByAreas().ToString();
            tbxTracerPercentBySlopes.Text = peaks.CalcTracerPercentByRatios().ToString();
            tbxScore.Text = peaks.DeconvolutionScore.ToString();
            double rtShift, rtStdDev;
            peaks.RetentionTimeShift(out rtShift, out rtStdDev);
            var retentionTimeShift = rtShift.ToString("#.00");
            if (!double.IsNaN(rtStdDev) && !double.IsInfinity(rtStdDev))
            {
                retentionTimeShift += "+/-" + rtStdDev.ToString("#.00");
            }
            tbxRetentionTimeShift.Text = retentionTimeShift;
        }

        private static double MaxInRange(IList<double> points, int startIndex, int endIndex)
        {
            double result = 0;
            for (int i = startIndex; i <= endIndex; i++)
            {
                result = Math.Max(result, points[i]);
            }
            return result;
        }

        public static Color GetColor(int iCandidate, int candidateCount)
        {
            var colors = new[]
                                       {
                                            Color.FromArgb(69,114,167),
                                            Color.FromArgb(170,70,67),
                                            Color.FromArgb(137,165,78),
                                            Color.FromArgb(113,88,143),
                                            Color.FromArgb(65,152,175),
                                            Color.FromArgb(219,132,61),
                                            Color.FromArgb(147,169,207),
                                       };
            if (iCandidate < colors.Length)
            {
                return colors[iCandidate];
            }

            if (candidateCount == 1)
            {
                return Color.FromArgb(0, 0, 255);
            }
            return Color.FromArgb(0, 255 * iCandidate / (candidateCount - 1),
                           255 * (candidateCount - iCandidate - 1) / (candidateCount - 1));

        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            UpdateUi();
        }

        private TracerChromatograms GetPoints()
        {
            return PeptideFileAnalysis.GetTracerChromatograms(Smooth);
        }

        private void cbxAutoFindPeak_CheckedChanged(object sender, EventArgs e)
        {
            SetAutoFindPeak(cbxAutoFindPeak.Checked);
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
        class PeakResize
        {
            public DbPeak Peak;
            public LineSegment LineSegment;
            public PeakDisplay PeakDisplay;
            public PointF MousePt;
            public PointF CoordPt;
            public Cursor GetCursor()
            {
                if (LineSegment == LineSegment.Middle)
                {
                    return Cursors.Hand;
                }
                return Cursors.SizeWE;
            }
        }

        class PeakDisplay
        {
            public LineItem HorizontalLine;
            public LineItem StartVerticalLine;
            public LineItem EndVerticalLine;
            public double Start;
            public double End;
            public double Height;
        }

        enum LineSegment
        {
            Start,
            Middle,
            End
        }

        private void btnAdjustPeaks_Click(object sender, EventArgs e)
        {
            var adjustPeaksMode = (AdjustPeaksMode) comboAdjustPeaks.SelectedIndex;
            var peaks = PeptideFileAnalysis.Peaks;
            foreach (var tracerFormula in peaks.ListChildren().Select(p=>p.TracerFormula))
            {
                if (!IsDisplayed(tracerFormula))
                {
                    continue;
                }
                var newPeaks = peaks.AutoSizePeak(tracerFormula, adjustPeaksMode);
                if (newPeaks.GetChildCount() == peaks.GetChildCount())
                {
                    peaks = newPeaks;
                }
                else
                {
                    newPeaks = newPeaks;
                }
            }
            PeptideFileAnalysis.SetDistributions(peaks);
        }

        private void cbxPeaksAsVerticalLines_CheckedChanged(object sender, EventArgs e)
        {
            UpdateUi();
        }

        public bool PeaksAsVerticalLines 
        { 
            get { return cbxPeaksAsVerticalLines.Checked; } 
            set { cbxPeaksAsVerticalLines.Checked = value;}
        }

        public bool PeaksAsHorizontalLines
        {
            get { return cbxPeaksAsHorizontalLines.Checked; }
            set { cbxPeaksAsHorizontalLines.Checked = value; }
        }

        private void cbxPeaksAsHorizontalLines_CheckedChanged(object sender, EventArgs e)
        {
            UpdateUi();
        }
    }
}
