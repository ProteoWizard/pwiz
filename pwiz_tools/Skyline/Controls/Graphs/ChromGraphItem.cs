/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using pwiz.MSGraph;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using ZedGraph;

namespace pwiz.Skyline.Controls.Graphs
{
    internal class ChromGraphItem : AbstractChromGraphItem
    {
        private const string FONT_FACE = "Arial";

        private static readonly Color COLOR_BEST_PEAK = Color.Black;
        private static readonly Color COLOR_RETENTION_TIME = Color.Gray;
        private static readonly Color COLOR_RETENTION_WINDOW = Color.LightGoldenrodYellow;
        private static readonly Color COLOR_BOUNDARIES = Color.Gray;
        private static readonly Color COLOR_BOUNDARIES_BEST = Color.Black;

        private const int MIN_BOUNDARY_DISPLAY_WIDTH = 7;
        private const int MIN_BEST_BOUNDARY_HEIGHT = 20;

        public static Color ColorSelected { get { return Color.Red; } }

        // private static readonly Color _colorNone = Color.Gray;

        // private static readonly FontSpec _fontSpecNone = CreateFontSpec(_colorNone);

        private static FontSpec CreateFontSpec(Color color, float size)
        {
            return new FontSpec(FONT_FACE, size, color, false, false, false, Color.Empty, null, FillType.None) {Border = {IsVisible = false}};
        }

        private readonly double[] _times;
        private readonly double[] _intensities;
        private readonly Color _color;
        private readonly FontSpec _fontSpec;
        private readonly int _width;

        private readonly Dictionary<double, int> _annotatedTimes = new Dictionary<double, int>();
        private readonly int[] _arrayLabelIndexes;
        private readonly double[] _libraryDotProducts;
        private readonly double _bestProduct;
        private readonly int _step;

        private int _bestPeakTimeIndex = -1;
        private PeakBoundsDragInfo _dragInfo;

        public ChromGraphItem(TransitionGroupDocNode transitionGroupNode,
                              TransitionDocNode transition,
                              ChromatogramInfo chromatogram,
                              TransitionChromInfo tranPeakInfo,
                              bool[] annotatePeaks,
                              double[] libraryDotProducts,
                              double bestProduct,
                              int step,
                              Color color,
                              float fontSize,
                              int width)
        {
            TransitionGroupNode = transitionGroupNode;
            TransitionNode = transition;
            Chromatogram = chromatogram;
            TransitionChromInfo = tranPeakInfo;

            _step = step;
            _color = color;
            _fontSpec = CreateFontSpec(color, fontSize);
            _width = width;

            // Cache values early to avoid accessing slow enumerators
            // which show up under profiling.
            Chromatogram.AsArrays(out _times, out _intensities);

            _libraryDotProducts = libraryDotProducts;
            _bestProduct = bestProduct;

            _arrayLabelIndexes = new int[annotatePeaks.Length];

            // Add peak times to hash set for labeling
            int iLastStart = 0;
            for (int i = 0; i < chromatogram.NumPeaks; i++)
            {
                int maxIndex = -1;
                if (annotatePeaks[i])
                {
                    ChromPeak peak = chromatogram.GetPeak(i);
                    if (!peak.IsForcedIntegration)
                        maxIndex = GetMaxIndex(peak.StartTime, peak.EndTime, ref iLastStart);
                }
                _arrayLabelIndexes[i] = maxIndex;
                if (maxIndex != -1 && !_annotatedTimes.ContainsKey(_times[maxIndex]))
                    _annotatedTimes.Add(_times[maxIndex], i);
            }

            // Calculate best peak index
            if (tranPeakInfo != null)
            {
                iLastStart = 0;
                _bestPeakTimeIndex = GetMaxIndex(tranPeakInfo.StartRetentionTime, tranPeakInfo.EndRetentionTime, ref iLastStart);
            }
        }

        public TransitionGroupDocNode TransitionGroupNode { get; private set; }
        public TransitionDocNode TransitionNode { get; private set; }
        public ChromatogramInfo Chromatogram { get; private set; }
        public TransitionChromInfo TransitionChromInfo { get; private set; }

        public double? RetentionPrediction { get; set; }
        public double RetentionWindow { get; set; }

        public bool HideBest { get; set; }

        public PeakBoundsDragInfo DragInfo
        {
            get { return _dragInfo; }
            set
            {
                _dragInfo = value;
                var tranPeakInfo = TransitionChromInfo;
                double startTime, endTime;
                if (_dragInfo != null)
                {
                    startTime = _dragInfo.StartTime;
                    endTime = _dragInfo.EndTime;
                }
                else if (tranPeakInfo != null)
                {
                    startTime = tranPeakInfo.StartRetentionTime;
                    endTime = tranPeakInfo.EndRetentionTime;                    
                }
                else
                {
                    // No best peak index
                    _bestPeakTimeIndex = -1;
                    return;
                }
                // Recalculate the best peak based on the changed information
                int iLastStart = 0;
                _bestPeakTimeIndex = GetMaxIndex(startTime, endTime, ref iLastStart);
            }
        }

        public override Color Color { get { return _color; } }
        public FontSpec FontSpec { get { return _fontSpec; } }

        public override void CustomizeCurve(CurveItem curveItem)
        {
            ((LineItem)curveItem).Line.Width = _width;
        }

        public static string GetTitle(TransitionDocNode nodeTran)
        {
            var tran = nodeTran.Transition;
            return string.Format("{0} - {1:F04}{2}", tran.FragmentIonName,
                                 nodeTran.Mz, Transition.GetChargeIndicator(tran.Charge));            
        }

        public static string GetTitle(TransitionGroupDocNode nodeGroup)
        {
            var seq = nodeGroup.TransitionGroup.Peptide.Sequence;
            return string.Format("{0} - {1:F04}{2}{3}", seq, nodeGroup.PrecursorMz,
                                 Transition.GetChargeIndicator(nodeGroup.TransitionGroup.PrecursorCharge),
                                 nodeGroup.TransitionGroup.LabelTypeText);            
        }

        public override string Title
        {
            get
            {
                if (_step != 0)
                    return string.Format("Step {0}", _step);

                return (TransitionNode == null?
                                                  GetTitle(TransitionGroupNode)
                            :
                                GetTitle(TransitionNode));
            }
        }

        public override IPointList Points
        {
            get
            {
                return new PointPairList(_times, _intensities);
            }
        }

        public override void AddAnnotations(MSGraphPane graphPane, Graphics g,
                                            MSPointList pointList, GraphObjList annotations)
        {
            // Draw retention time indicator, if set
            if (RetentionPrediction.HasValue)
            {
                double time = RetentionPrediction.Value;
                double xTemp, yMax;
                PointF ptTop = new PointF(0, graphPane.Chart.Rect.Top);
                graphPane.ReverseTransform(ptTop, out xTemp, out yMax);

                // Create temporary label to calculate positions
                string label = string.Format("Predicted\n{0:F01}", time);
                FontSpec fontLabel = CreateFontSpec(COLOR_RETENTION_TIME, _fontSpec.Size);
                SizeF sizeLabel = fontLabel.MeasureString(g, label, graphPane.CalcScaleFactor());
                ptTop = new PointF(0, ptTop.Y + sizeLabel.Height + 10);

                double intensity;
                graphPane.ReverseTransform(ptTop, out xTemp, out intensity);

                LineObj stick = new LineObj(COLOR_RETENTION_TIME, time, intensity, time, 0)
                                    {
                                        IsClippedToChartRect = true,
                                        Location = {CoordinateFrame = CoordType.AxisXYScale},
                                        ZOrder = ZOrder.B_BehindLegend,
                                        Line = { Width = 1}
                                    };
                annotations.Add(stick);

                if (GraphChromatogram.ShowRT != ShowRTChrom.none)
                {
                    ptTop = new PointF(0, ptTop.Y - 5);
                    graphPane.ReverseTransform(ptTop, out xTemp, out intensity);
                    TextObj text = new TextObj(label, time, intensity,
                                               CoordType.AxisXYScale, AlignH.Center, AlignV.Bottom)
                                       {
                                           IsClippedToChartRect = true,
                                           ZOrder = ZOrder.B_BehindLegend,
                                           FontSpec = CreateFontSpec(COLOR_RETENTION_TIME, _fontSpec.Size),
                                       };
                    annotations.Add(text);
                }

                // Draw background for retention time window
                if (RetentionWindow > 0)
                {
                    double boxHeight = yMax;
                    BoxObj box = new BoxObj(time - RetentionWindow / 2, boxHeight, RetentionWindow, boxHeight,
                                            COLOR_RETENTION_WINDOW, COLOR_RETENTION_WINDOW)
                                     {
                                         IsClippedToChartRect = true,
                                         ZOrder = ZOrder.F_BehindGrid
                                     };
                    annotations.Add(box);
                }
            }

            if ((DragInfo != null || (!HideBest && TransitionChromInfo != null)))
            {
                // Show text and arrow for the best peak
                double intensityBest = 0;
                if (_bestPeakTimeIndex != -1)
                {
                    double timeBest = _times[_bestPeakTimeIndex];
                    float xBest = graphPane.XAxis.Scale.Transform(timeBest);
                    intensityBest = _intensities[_bestPeakTimeIndex];
                    float yBest = graphPane.YAxis.Scale.Transform(intensityBest);

                    if (GraphChromatogram.ShowRT != ShowRTChrom.none || DragInfo != null)
                    {
                        // Best peak gets its own label to avoid curve overlap detection
                        double intensityLabel = graphPane.YAxis.Scale.ReverseTransform(yBest - 5);
                        string label = FormatTimeLabel(timeBest, _libraryDotProducts != null ? _bestProduct : 0);
                        TextObj text = new TextObj(label, timeBest, intensityLabel,
                                                   CoordType.AxisXYScale, AlignH.Center, AlignV.Bottom)
                                           {
                                               ZOrder = ZOrder.A_InFront,
                                               IsClippedToChartRect = true,
                                               FontSpec = FontSpec
                                           };
                        annotations.Add(text);
                    }

                    // Show the best peak arrow indicator
                    double timeArrow = graphPane.XAxis.Scale.ReverseTransform(xBest - 4);
                    double intensityArrow = graphPane.YAxis.Scale.ReverseTransform(yBest - 2);

                    ArrowObj arrow = new ArrowObj(COLOR_BEST_PEAK, 12f,
                                                  timeArrow, intensityArrow, timeArrow, intensityArrow)
                                         {
                                             Location = { CoordinateFrame = CoordType.AxisXYScale },
                                             IsArrowHead = true,
                                             IsClippedToChartRect = true,
                                             ZOrder = ZOrder.A_InFront
                                         };
                    annotations.Add(arrow);                    
                }

                // Show the best peak boundary lines
                double startTime, endTime;
                if (DragInfo != null)
                {
                    startTime = DragInfo.StartTime;
                    endTime = DragInfo.EndTime;
                }
                else
                {
                    var tranPeakInfo = TransitionChromInfo;
                    startTime = tranPeakInfo.StartRetentionTime;
                    endTime = tranPeakInfo.EndRetentionTime;                    
                }
                AddPeakBoundaries(graphPane, annotations, true,
                                  startTime, endTime, intensityBest);
            }

            for (int i = 0, len = Chromatogram.NumPeaks; i < len; i++)
            {
                if (_arrayLabelIndexes[i] == -1)
                    continue;

                ChromPeak peak = Chromatogram.GetPeak(i);
                if (peak.IsForcedIntegration)
                    continue;

                double maxIntensity = _intensities[_arrayLabelIndexes[i]];

                // Show peak extent indicators, if they are far enough apart
                AddPeakBoundaries(graphPane, annotations, false,
                                  peak.StartTime, peak.EndTime, maxIntensity);
            }
        }

        public double GetMaxIntensity(double startTime, double endTime)
        {
            int iLastStart = 0;
            int iMax = GetMaxIndex(startTime, endTime, ref iLastStart);
            return (iMax != -1 ? _intensities[iMax] : 0);
        }

        private int GetMaxIndex(double startTime, double endTime, ref int iLastStart)
        {
            // Search forward for the start of the peak
            // This position is retained as the start of the next search, since
            // peaks are not guaranteed to be non-overlapping.
            while (iLastStart < _times.Length && _times[iLastStart] < startTime)
                iLastStart++;
            // Search forward for the maximum intensity until the end of the peak is reached
            int maxIndex = -1;
            double maxIntensity = 0;
            for (int iPoint = iLastStart; iPoint < _times.Length && _times[iPoint] < endTime; iPoint++)
            {
                if (_intensities[iPoint] > maxIntensity)
                {
                    maxIntensity = _intensities[iPoint];
                    maxIndex = iPoint;
                }
            }
            return maxIndex;
        }

        private void AddPeakBoundaries(GraphPane graphPane, ICollection<GraphObj> annotations,
                                       bool best, double startTime, double endTime, double maxIntensity)
        {
            // Only show boundaries for dragging when boundaries turned off
            if (!Settings.Default.ShowPeakBoundaries && (!best || DragInfo == null))
                return;

            float xStart = graphPane.XAxis.Scale.Transform(startTime);
            float xEnd = graphPane.XAxis.Scale.Transform(endTime);
            // Hide boundaries, if they are too close together
            if (xEnd - xStart <= MIN_BOUNDARY_DISPLAY_WIDTH)
            {
                // But not if they are currently part of a drag operation.
                if (DragInfo == null)
                    return;                
            }

            // Make sure the best borders are visible
            if (best)
            {
                float yMax = graphPane.YAxis.Scale.Transform(maxIntensity);
                float yZero = graphPane.YAxis.Scale.Transform(0);
                if (yZero - yMax < MIN_BEST_BOUNDARY_HEIGHT)
                    maxIntensity = graphPane.YAxis.Scale.ReverseTransform(yZero - MIN_BEST_BOUNDARY_HEIGHT);
            }

            Color colorBoundaries = (best ? COLOR_BOUNDARIES_BEST : COLOR_BOUNDARIES);

            // Make sure to get maximum intensity within the peak range,
            // as this is not guaranteed to be the center of the peak
            LineObj stickStart = new LineObj(colorBoundaries, startTime, maxIntensity, startTime, 0)
                                     {
                                         IsClippedToChartRect = true,
                                         Location = { CoordinateFrame = CoordType.AxisXYScale },
                                         ZOrder = ZOrder.B_BehindLegend,
                                         Line = { Width = 1, Style = DashStyle.Dash }
                                     };
            annotations.Add(stickStart);
            LineObj stickEnd = new LineObj(colorBoundaries, endTime, maxIntensity, endTime, 0)
                                   {
                                       IsClippedToChartRect = true,
                                       Location = { CoordinateFrame = CoordType.AxisXYScale },
                                       ZOrder = ZOrder.B_BehindLegend,
                                       Line = { Width = 1, Style = DashStyle.Dash }
                                   };
            annotations.Add(stickEnd);
        }

        public override PointAnnotation AnnotatePoint(PointPair point)
        {
            int indexPeak;
            var showRT = GraphChromatogram.ShowRT;
            if ((showRT == ShowRTChrom.all ||
                 (showRT == ShowRTChrom.threshold && Settings.Default.ShowRetentionTimesThreshold <= point.Y))
                && _annotatedTimes.TryGetValue(point.X, out indexPeak))
            {
                string label = FormatTimeLabel(point.X, _libraryDotProducts != null ?
                                                                                        _libraryDotProducts[indexPeak] : 0);
                return new PointAnnotation(label, FontSpec);
            }

            return null;
        }

        public string FormatTimeLabel(double time, double libraryProduct)
        {
            string label = string.Format("{0:F01}", time);
            if (libraryProduct != 0)
                label += string.Format("\n(dotp {0:F02})", libraryProduct);
            return label;
        }

        public double FindPeakRetentionTime(TextObj label)
        {
            // Make sure the label came from this graph item
            if (ReferenceEquals(FontSpec, label.FontSpec))
            {
                // Parse the time out of the label
                double time;                
                if (double.TryParse(label.Text.Split('\n')[0], out time))
                {
                    // Search for a time that corresponds with the label
                    for (int i = 0; i < _arrayLabelIndexes.Length; i++)
                    {
                        int iTime = _arrayLabelIndexes[i];
                        if (iTime != -1 && Math.Abs(time - _times[iTime]) < 0.15)
                            return _times[iTime];
                    }
                }                
            }
            return 0;
        }

        public double GetNearesBestPeakBoundary(double time)
        {
            var tranPeakInfo = TransitionChromInfo;
            if (tranPeakInfo == null)
                return 0;

            double deltaStart = Math.Abs(tranPeakInfo.StartRetentionTime - time);
            double deltaEnd = Math.Abs(tranPeakInfo.EndRetentionTime - time);
            return deltaStart < deltaEnd ? tranPeakInfo.StartRetentionTime : tranPeakInfo.EndRetentionTime;
        }

        public double GetNearestRetentionTime(double time)
        {
            int iTime = Array.BinarySearch(_times, time);
            if (iTime < 0)
            {
                // Get index of first time greater than time argument
                iTime = ~iTime;
                // If the value before it was closer, then use that time
                if (iTime == _times.Length || (iTime > 0 && _times[iTime] - time > time - _times[iTime - 1]))
                    iTime--;
            }
            return _times[iTime];
        }
    }

    public sealed class FailedChromGraphItem : NoDataChromGraphItem
    {
        public FailedChromGraphItem(TransitionGroupDocNode nodeGroup, Exception x)
            : base(string.Format("{0} (load failed: {1})", ChromGraphItem.GetTitle(nodeGroup), x.Message))
        {            
        }
    }

    public sealed class NotFoundChromGraphItem : NoDataChromGraphItem
    {
        public NotFoundChromGraphItem(TransitionDocNode nodeTran)
            : base(string.Format("{0} (not found)", ChromGraphItem.GetTitle(nodeTran)))
        {
        }

        public NotFoundChromGraphItem(TransitionGroupDocNode nodeGroup)
            : base(string.Format("{0} (not found)", ChromGraphItem.GetTitle(nodeGroup)))
        {
        }
    }

    public sealed class UnavailableChromGraphItem : NoDataMSGraphItem
    {
        public UnavailableChromGraphItem() : base("Chromatogram information unavailable")
        {
        }
    }

    public class NoDataChromGraphItem : AbstractChromGraphItem
    {
        private readonly string _title;

        public NoDataChromGraphItem(string title)
        {
            _title = title;
        }

        public override string Title { get { return _title; } }

        public override PointAnnotation AnnotatePoint(PointPair point)
        {
            return null;
        }

        public override void AddAnnotations(MSGraphPane graphPane, Graphics g, MSPointList pointList, GraphObjList annotations)
        {
            // Do nothing
        }

        public override IPointList Points
        {
            get
            {
                return new PointPairList(new double[0], new double[0]);
            }
        }
    }

    public abstract class AbstractChromGraphItem : IMSGraphItemExtended
    {
        public abstract string Title { get; }
        public abstract PointAnnotation AnnotatePoint(PointPair point);
        public abstract void AddAnnotations(MSGraphPane graphPane, Graphics g,
                                            MSPointList pointList, GraphObjList annotations);
        public abstract IPointList Points { get; }

        public virtual Color Color
        {
            get { return Color.Gray; }
        }

        public virtual void CustomizeCurve(CurveItem curveItem)
        {
            // Do nothing by default
        }

        public MSGraphItemType GraphItemType
        {
            get { return MSGraphItemType.Chromatogram; }
        }

        public MSGraphItemDrawMethod GraphItemDrawMethod
        {
            get { return MSGraphItemDrawMethod.Line; }
        }

        public void CustomizeYAxis(Axis axis)
        {
            CustomizeAxis(axis, "Intensity");
        }

        public void CustomizeXAxis(Axis axis)
        {
            CustomizeAxis(axis, "Retention Time");
        }

        private static void CustomizeAxis(Axis axis, string title)
        {
            axis.Title.FontSpec.Family = "Arial";
            axis.Title.FontSpec.Size = 14;
            axis.Color = axis.Title.FontSpec.FontColor = Color.Black;
            axis.Title.FontSpec.Border.IsVisible = false;
            axis.Title.Text = title;
        }
    }
}