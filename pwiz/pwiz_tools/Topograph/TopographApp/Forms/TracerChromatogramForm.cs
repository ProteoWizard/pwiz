/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011 University of Washington - Seattle, WA
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
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using pwiz.Topograph.Enrichment;
using pwiz.Topograph.Model;
using pwiz.Topograph.ui.Properties;
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
            splitContainer1.Panel2.Controls.Add(MsGraphControl);
            colAreaPct.DefaultCellStyle.Format = "0.##%";
            colSlopePct.DefaultCellStyle.Format = "0.##%";
            colTracerPercent.DefaultCellStyle.Format = "0.##%";
            comboAdjustPeaks.SelectedIndex = 0;
            cbxPeaksAsVerticalLines.Checked = Settings.Default.PeaksAsVerticalLines;
            cbxPeaksAsHorizontalLines.Checked = Settings.Default.PeaksAsHorizontalLines;
            cbxShowScore.Checked = Settings.Default.ShowChromatogramScore;
        }

        protected override bool MsGraphControlOnMouseDownEvent(ZedGraphControl sender, MouseEventArgs e)
        {
            _peakResize = null;
            if (base.MsGraphControlOnMouseDownEvent(sender, e))
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

        protected override bool MsGraphControlOnDoubleClickEvent(ZedGraphControl sender, MouseEventArgs e)
        {
            var peakResize = PeakResizeFromPoint(e.Location);
            if (peakResize != null)
            {
                var peaks = PeptideFileAnalysis.CalculatedPeaks;

                var newPeaks = peaks.AutoSizePeak(peakResize.TracerFormula, (AdjustPeaksMode)comboAdjustPeaks.SelectedIndex);
                if (newPeaks != null)
                {
                    PeptideFileAnalysis.SetCalculatedPeaks(newPeaks);
                }
                return true;
            }
            return base.MsGraphControlOnDoubleClickEvent(sender, e);
        }

        protected override bool MsGraphControlOnMouseUpEvent(ZedGraphControl sender, MouseEventArgs e)
        {
            if (_peakResize != null)
            {
                var point = new PointF(e.X - _peakResize.MousePt.X + _peakResize.CoordPt.X, e.Y);
                double x, y;
                MsGraphControl.GraphPane.ReverseTransform(point, out x, out y);
                var peaks = PeptideFileAnalysis.CalculatedPeaks;
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
                        newStart = x;
                        newEnd = x - _peakResize.Peak.StartTime + _peakResize.Peak.EndTime;
                        break;
                }
                var newPeaks = peaks.ChangeTime(_peakResize.TracerFormula, Math.Min(newStart, newEnd), Math.Max(newStart, newEnd));
                _peakResize = null;
                PeptideFileAnalysis.SetCalculatedPeaks(newPeaks);
                PeptideAnalysis.EnsurePeaksCalculated();
            }
            return base.MsGraphControlOnMouseUpEvent(sender, e);
        }

        protected override bool MsGraphControlOnMouseMoveEvent(ZedGraphControl sender, MouseEventArgs e)
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
                MsGraphControl.GraphPane.ReverseTransform(screenPt, out x, out y);
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
                MsGraphControl.Invalidate();
                sender.Cursor = _peakResize.GetCursor();
                return true;
            }
            if (base.MsGraphControlOnMouseMoveEvent(sender, e))
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
            var peaks = PeptideFileAnalysis.CalculatedPeaks;
            if (peaks == null)
            {
                return null;
            }
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
                    TracerFormula = entry.Key,
                    Peak = peak.Value,
                    MousePt = pointF,
                    PeakDisplay = peakDisplay,
                };
                if (peakDisplay.HorizontalLine != null)
                {
                    PointF startPt = MsGraphControl.GraphPane.GeneralTransform(peakDisplay.HorizontalLine.Points[0], CoordType.AxisXYScale);
                    PointF endPt = MsGraphControl.GraphPane.GeneralTransform(peakDisplay.HorizontalLine.Points[1],
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
                    PointF firstPt = MsGraphControl.GraphPane.GeneralTransform(lineItemSeg.Key.Points[0],
                                                                               CoordType.AxisXYScale);
                    PointF lastPt = MsGraphControl.GraphPane.GeneralTransform(lineItemSeg.Key.Points[1],
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
            cbxSmooth.Checked = Smooth;
            MsGraphControl.GraphPane.GraphObjList.Clear();
            MsGraphControl.GraphPane.CurveList.Clear();
            _peakLines = null;
            if (!PeptideFileAnalysis.IsMzKeySetComplete(PeptideFileAnalysis.ChromatogramSet.Chromatograms.Keys))
            {
                return;
            }
            var overlayFileAnalysis = UpdateDataFileCombo(comboOverlay);
            var peakLines = new Dictionary<TracerFormula, PeakDisplay>();
            var tracerChromatograms = GetPoints();
            TracerChromatograms overlayTracerChromatograms = null;
            double[] mappedTimes = null;
            if (overlayFileAnalysis != null)
            {
                overlayTracerChromatograms = overlayFileAnalysis.GetTracerChromatograms(Smooth);
                var retentionTimeAlignment =
                    overlayFileAnalysis.MsDataFile.GetRetentionTimeAlignment(PeptideFileAnalysis.MsDataFile);
                mappedTimes =
                    overlayTracerChromatograms.Times.Select(retentionTimeAlignment.GetTargetTime).ToArray();
            }
            var peaks = PeptideFileAnalysis.CalculatedPeaks;
            var entries = tracerChromatograms.Points.ToArray();
            if (dataGridView1.Rows.Count != entries.Length)
            {
                dataGridView1.Rows.Clear();
                dataGridView1.Rows.Add(entries.Length);
            }
            if (cbxShowScore.Checked)
            {
                var scoreLine = MsGraphControl.GraphPane.AddCurve("Score", tracerChromatograms.Times.ToArray(), tracerChromatograms.Scores.ToArray(), Color.Black, SymbolType.None);
                scoreLine.IsY2Axis = true;
                scoreLine.Line.Width = Settings.Default.ChromatogramLineWidth;
            }
            double totalAmount = peaks.Peaks.Values.Sum(p => p.Area);
            double totalSlope = peaks.Peaks.Values.Sum(p => p.RatioToBase);
            for (int iCandidate = 0; iCandidate < entries.Count(); iCandidate++)
            {
                var entry = entries[iCandidate];
                var label = entry.Key.ToDisplayString();
                var row = dataGridView1.Rows[iCandidate];
                row.Cells[colFormula.Index].Value = label;
                var peak = peaks.GetPeak(entry.Key);
                if (peak != null)
                {
                    row.Cells[colAreaPct.Index].Value = peak.Value.Area/totalAmount;
                    row.Cells[colSlopePct.Index].Value = peak.Value.RatioToBase/totalSlope;
                    row.Cells[colStartTime.Index].Value = peak.Value.StartTime;
                    row.Cells[colEndTime.Index].Value = peak.Value.EndTime;
                    row.Cells[colArea.Index].Value = peak.Value.Area;
                    row.Cells[colCorr.Index].Value = peak.Value.Correlation;
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
                        Title = label,
                        Color = GetColor(iCandidate, entries.Length),
                        Points = new PointPairList(tracerChromatograms.Times, entry.Value),
                    };
                    var chromCurveItem = (LineItem) MsGraphControl.AddGraphItem(MsGraphControl.GraphPane, curve);
                    chromCurveItem.Label.IsVisible = false;
                    chromCurveItem.Line.Width = Settings.Default.ChromatogramLineWidth;
                    if (overlayTracerChromatograms != null)
                    {
                        var overlayCurve = new ChromatogramGraphItem
                                               {
                                                   Title = overlayFileAnalysis.MsDataFile.Label + " " + label,
                                                   Color = GetColor(iCandidate, entries.Length),
                                                   Points =
                                                       new PointPairList(mappedTimes,
                                                                         overlayTracerChromatograms.Points[entry.Key])
                                               };
                        var overlayCurveItem = (LineItem) MsGraphControl.AddGraphItem(MsGraphControl.GraphPane, overlayCurve);
                        overlayCurveItem.Label.IsVisible = false;
                        overlayCurveItem.Line.Style = DashStyle.Dash;
                        overlayCurveItem.Line.Width = Settings.Default.ChromatogramLineWidth;
                    }
                    if (peak != null)
                    {
                        var peakDisplay = new PeakDisplay();
                        var color = GetColor(iCandidate, entries.Length);
                        var max = MaxInRange(entry.Value, tracerChromatograms.ChromatogramSet.IndexFromTime(peak.Value.StartTime), 
                            tracerChromatograms.ChromatogramSet.IndexFromTime(peak.Value.EndTime));
                        peakDisplay.Start = peak.Value.StartTime;
                        peakDisplay.End = peak.Value.EndTime;
                        peakDisplay.Height = max;
                        if (PeaksAsHorizontalLines)
                        {
                            peakDisplay.HorizontalLine = MsGraphControl.GraphPane.AddCurve(null, new[] { peakDisplay.Start, peakDisplay.End }, new[] { max, max }, color);
                            peakDisplay.HorizontalLine.Line.Width = Settings.Default.ChromatogramLineWidth;
                        }
                        if (PeaksAsVerticalLines)
                        {
                            peakDisplay.StartVerticalLine = MsGraphControl.GraphPane.AddCurve(
                                null,
                                new[] { peakDisplay.Start, peakDisplay.Start },
                                new[] {max, 0}, color, SymbolType.None
                            );
                            peakDisplay.StartVerticalLine.Line.Width = Settings.Default.ChromatogramLineWidth;
                            peakDisplay.EndVerticalLine = MsGraphControl.GraphPane.AddCurve(
                                null,
                                new[] { peakDisplay.End, peakDisplay.End }, 
                                new[] { max, 0 }, color, SymbolType.None
                            );
                            peakDisplay.EndVerticalLine.Line.Width = Settings.Default.ChromatogramLineWidth;
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
            if (PeptideFileAnalysis.PsmTimes != null)
            {
                foreach (var time in PeptideFileAnalysis.PsmTimes.SelectMany(entry=>entry.Value))
                {
                    MsGraphControl.GraphPane.GraphObjList.Add(new LineObj(Color.LightBlue, time, 0, time,
                                                                          1)
                    {
                        Line = { Style = DashStyle.DashDot },
                        ZOrder = ZOrder.E_BehindCurves,
                        IsClippedToChartRect = true,
                        Location = { CoordinateFrame = CoordType.XScaleYChartFraction }
                    });
                    
                }
            }
            else
            {
                var otherPeaks = PeptideAnalysis.FileAnalyses.Select(f => f.CalculatedPeaks);
                double firstDetectedTime, lastDetectedTime;
                PeptideFileAnalysis.CalculatedPeaks.GetFirstLastTimes(otherPeaks, out firstDetectedTime, out lastDetectedTime);
                MsGraphControl.GraphPane.GraphObjList.Add(new LineObj(Color.Black, firstDetectedTime, 0,
                                                                      firstDetectedTime, 1)
                                                              {
                                                                  Line = {Style = DashStyle.DashDotDot},
                                                                  ZOrder = ZOrder.E_BehindCurves,
                                                                  IsClippedToChartRect = true,
                                                                  Location = { CoordinateFrame = CoordType.XScaleYChartFraction }
                                                              });
                MsGraphControl.GraphPane.GraphObjList.Add(new LineObj(Color.Black, lastDetectedTime, 0,
                                                                      lastDetectedTime, 1)
                    {
                        Line = { Style = DashStyle.DashDotDot },
                        ZOrder = ZOrder.E_BehindCurves,
                        IsClippedToChartRect = true,
                        Location = { CoordinateFrame = CoordType.XScaleYChartFraction }
                    });
            }

            MsGraphControl.AxisChange();
            MsGraphControl.Invalidate();
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
                                            Color.FromArgb(56,93,138),
                                            Color.FromArgb(140,56,54),
                                            Color.FromArgb(113,137,63),
                                            Color.FromArgb(92,71,118),
                                            Color.FromArgb(53,125,145),
                                            Color.FromArgb(182,109,49),
                                            Color.FromArgb(66,109,161),
                                            Color.FromArgb(164,67,64),
                                            Color.FromArgb(132,159,75),
                                            Color.FromArgb(108,84,138),//10
                                            Color.FromArgb(63,146,169),
                                            Color.FromArgb(211,127,58),
                                            Color.FromArgb(75,123,180),
                                            Color.FromArgb(183,76,73),
                                            Color.FromArgb(148,178,85),
                                            Color.FromArgb(122,95,154),
                                            Color.FromArgb(71,164,189),
                                            Color.FromArgb(236,143,66),
                                            Color.FromArgb(115,148,197),
                                            Color.FromArgb(200,115,114),//20
                                            Color.FromArgb(169,195,121),
                                            Color.FromArgb(148,128,174),
                                            Color.FromArgb(112,183,205),
                                            Color.FromArgb(248,165,110),
                                            Color.FromArgb(161,180,212),
                                            Color.FromArgb(214,161,160),
                                            Color.FromArgb(192,210,164),
                                            Color.FromArgb(179,168,196),
                                            Color.FromArgb(160,202,217),
                                            Color.FromArgb(249,190,158),//30
                                            Color.FromArgb(194,205,225),
                                            Color.FromArgb(226,194,194),
                                            Color.FromArgb(192,80,77),
                                            Color.FromArgb(155,187,89),
                                            Color.FromArgb(128,100,162),
                                            Color.FromArgb(75,172,198),
                                            Color.FromArgb(247,150,70),
                                            Color.FromArgb(118,150,198),
                                            Color.FromArgb(200,118,116),
                                            Color.FromArgb(170,196,123),//40
                                            Color.FromArgb(149,130,176),
                                            Color.FromArgb(248,166,113),
                                            Color.FromArgb(147,169,207),
                                            Color.FromArgb(209,147,146),
                                            Color.FromArgb(185,205,150),
                                            Color.FromArgb(169,155,189),
                                            Color.FromArgb(145,195,213),
                                            Color.FromArgb(249,181,144),
                                            Color.FromArgb(170,186,215),
                                            Color.FromArgb(217,170,169),//50
                                            Color.FromArgb(198,214,172),
                                            Color.FromArgb(186,176,201),
                                            Color.FromArgb(169,206,220),
                                            Color.FromArgb(250,195,168),
                                            Color.FromArgb(209,222,190),
                                            Color.FromArgb(200,192,212),
                                            Color.FromArgb(187,215,227),
                                            Color.FromArgb(251,207,186),
                                            Color.FromArgb(205,214,230),
                                       };
            return colors[iCandidate % colors.Length];
        }

        public static DashStyle GetDashStyle(int i)
        {
            var dashStyles = new[]
                                 {
                                     DashStyle.Solid,
                                     DashStyle.Dash,
                                     DashStyle.DashDot,
                                     DashStyle.DashDotDot,
                                     DashStyle.Dot,
                                 };
            return dashStyles[i%dashStyles.Length];
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
            public TracerFormula TracerFormula;
            public CalculatedPeaks.Peak Peak;
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
            var peaks = PeptideFileAnalysis.CalculatedPeaks;
            foreach (var tracerFormula in peaks.Peaks.Keys)
            {
                if (!IsDisplayed(tracerFormula))
                {
                    continue;
                }
                var newPeaks = peaks.AutoSizePeak(tracerFormula, adjustPeaksMode);
                if (newPeaks.Peaks.Count == peaks.Peaks.Count)
                {
                    peaks = newPeaks;
                }
                else
                {
                    newPeaks = newPeaks;
                }
            }
            PeptideFileAnalysis.SetCalculatedPeaks(peaks);
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

        private void cbxSmooth_CheckedChanged(object sender, EventArgs e)
        {
            Smooth = cbxSmooth.Checked;
        }

        private void comboOverlay_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateUi();
        }
    }
}
