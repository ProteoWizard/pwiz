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
using System.Linq;
using pwiz.Common.Chemistry;
using pwiz.MSGraph;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Themes;
using pwiz.Skyline.Properties;
using ZedGraph;

namespace pwiz.Skyline.Controls.Graphs
{
    public sealed class ChromGraphItem : AbstractChromGraphItem
    {
        private const string FONT_FACE = "Arial";

        private static readonly Color COLOR_BEST_PEAK = Color.Black;
        private static readonly Color COLOR_RETENTION_TIME = Color.Gray;
        private static readonly Color COLOR_MSMSID_TIME = Color.Navy;
        private static readonly Color COLOR_ALIGNED_MSMSID_TIME = Color.LightBlue;
        private static readonly Color COLOR_UNALIGNED_MSMSID_TIME = Color.Cyan;
        private static readonly Color COLOR_RETENTION_WINDOW = Color.LightGoldenrodYellow;
        private static readonly Color COLOR_BOUNDARIES = Color.LightGray;
        private static readonly Color COLOR_BOUNDARIES_BEST = Color.Black;
        public static readonly Color COLOR_ORIGINAL_PEAK_SHADE = Color.FromArgb(30, Color.BlueViolet);

        private const int MIN_BOUNDARY_DISPLAY_WIDTH = 7;
        private const int MIN_BEST_BOUNDARY_HEIGHT = 20;

        private static FontSpec CreateFontSpec(Color color, float size)
        {
            var fontSpec = new FontSpec(FONT_FACE, size, color, false, false, false)
            {
                Fill = new Fill(Color.FromArgb(0xC0, 0xff, 0xff, 0xff)),
                Border = new Border(Color.FromArgb(0x40, 0xff, 0xff, 0xff), 2.0f)
            };
            return fontSpec;
        }

        private readonly double[] _measuredTimes;
        private readonly double[] _displayTimes;
        private readonly double[] _intensities;
        private readonly FontSpec _fontSpec;
        private readonly int _width;

        private readonly Dictionary<int, int> _annotatedTimes = new Dictionary<int, int>();
        private readonly int[] _arrayLabelIndexes;
        private readonly double[] _dotProducts;
        private readonly double _bestProduct;
        private readonly bool _isFullScanMs;
        private readonly bool _isSummary;
        private readonly RawTimesInfoItem? _displayRawTimes;
        private readonly int _step;

        private int _bestPeakTimeIndex = -1;
        private PeakBoundsDragInfo _dragInfo;

        public ChromGraphItem(TransitionGroupDocNode transitionGroupNode,
                              TransitionDocNode transition,
                              ChromatogramInfo chromatogram,
                              TransitionChromInfo tranPeakInfo,
                              RegressionLine timeRegressionFunction,
                              bool[] annotatePeaks,
                              double[] dotProducts,
                              double bestProduct,
                              bool isFullScanMs,
                              bool isSummary,
                              RawTimesInfoItem? displayRawTimes,
                              int step,
                              Color color,
                              float fontSize,
                              int width,
                              FullScanInfo fullScanInfo = null)
        {
            TransitionGroupNode = transitionGroupNode;
            TransitionNode = transition;
            Chromatogram = chromatogram;
            TransitionChromInfo = tranPeakInfo;
            TimeRegressionFunction = timeRegressionFunction;
            Color = color;
            FullScanInfo = fullScanInfo;

            _step = step;
            _fontSpec = CreateFontSpec(color, fontSize);
            _width = width;

            _dotProducts = dotProducts;
            _bestProduct = bestProduct;
            _isFullScanMs = isFullScanMs;
            _isSummary = isSummary;
            _displayRawTimes = displayRawTimes;

            _arrayLabelIndexes = new int[annotatePeaks.Length];

            if (chromatogram == null)
            {
                _measuredTimes = new double[0];
                _displayTimes = _measuredTimes;
                _intensities = new double[0];
            }
            else
            {
                // Cache values early to avoid accessing slow enumerators
                // which show up under profiling.
                Chromatogram.AsArrays(out _measuredTimes, out _intensities);
                if (TimeRegressionFunction == null)
                {
                    _displayTimes = _measuredTimes;
                }
                else
                {
                    _displayTimes = _measuredTimes.Select(TimeRegressionFunction.GetY).ToArray();
                }
                // Add peak times to hash set for labeling
                int iLastStart = 0;
                for (int i = 0; i < chromatogram.NumPeaks; i++)
                {
                    int maxIndex = -1;
                    if (annotatePeaks[i])
                    {
                        ChromPeak peak = chromatogram.GetPeak(i);
                        maxIndex = GetMaxIndex(peak.StartTime, peak.EndTime, ref iLastStart);
                    }
                    _arrayLabelIndexes[i] = maxIndex;
                    if (maxIndex != -1 && !_annotatedTimes.ContainsKey(maxIndex))
                        _annotatedTimes.Add(maxIndex, i);
                }

                // Calculate best peak index
                if (tranPeakInfo != null)
                {
                    iLastStart = 0;
                    _bestPeakTimeIndex = GetMaxIndex(tranPeakInfo.StartRetentionTime, tranPeakInfo.EndRetentionTime, ref iLastStart);
                }
            }
        }

        public FullScanInfo FullScanInfo { get; private set; }
        public TransitionGroupDocNode TransitionGroupNode { get; private set; }
        public TransitionDocNode TransitionNode { get; private set; }
        public ChromatogramInfo Chromatogram { get; private set; }
        public TransitionChromInfo TransitionChromInfo { get; private set; }
        public RegressionLine TimeRegressionFunction { get; private set; }
        public ScaledRetentionTime ScaleRetentionTime(double measuredTime)
        {
            return new ScaledRetentionTime(measuredTime, MeasuredTimeToDisplayTime(measuredTime));
        }
        public int OptimizationStep { get { return _step; } }

        public double? RetentionPrediction { get; set; }
        public ExplicitRetentionTimeInfo RetentionExplicit { get; set; }
        public double RetentionWindow { get; set; }

        public double[] RetentionMsMs { get; set; }
        public double[] MidasRetentionMsMs { get; set; }
        public double? SelectedRetentionMsMs { get; set; }

        public double[] AlignedRetentionMsMs { get; set; }
        public double[] UnalignedRetentionMsMs { get; set; }

        public bool HideBest { get; set; }

        public double BestPeakTime { get { return _bestPeakTimeIndex != -1 ? _measuredTimes[_bestPeakTimeIndex] : 0; } }

        public string CurveAnnotation { get; set; }
        public PeptideGraphInfo GraphInfo { get; set; }
        public IdentityPath IdPath { get; set; }
        public DashStyle? LineDashStyle { get; set; }

        internal PeakBoundsDragInfo DragInfo
        {
            get { return _dragInfo; }
            set
            {
                _dragInfo = value;
                var tranPeakInfo = TransitionChromInfo;
                double startTime, endTime;
                if (_dragInfo != null)
                {
                    startTime = _dragInfo.StartTime.MeasuredTime;
                    endTime = _dragInfo.EndTime.MeasuredTime;
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

        public FontSpec FontSpec { get { return _fontSpec; } }

        public override void CustomizeCurve(CurveItem curveItem)
        {
            ((LineItem)curveItem).Line.Width = _width;
            if (LineDashStyle.HasValue)
            {
                ((LineItem)curveItem).Line.Style = LineDashStyle.Value;
            }
        }

        public static string GetTitle(TransitionGroupDocNode transitionGroup, TransitionDocNode transition)
        {
            if (null == transition)
            {
                return GetTitle(transitionGroup);
            }
            return string.Format(@"{0}{1} - {2:F04}{3}{4}", transition.FragmentIonName,
                                 Transition.GetMassIndexText(transition.Transition.MassIndex),
                                 transition.Mz,
                                 Transition.GetChargeIndicator(transition.Transition.Adduct),
                                 transitionGroup.TransitionGroup.LabelTypeText);
        }
        
        public static string GetTitle(TransitionDocNode nodeTran)
        {
            var tran = nodeTran.Transition;
            return string.Format(@"{0}{1} - {2:F04}{3}", nodeTran.FragmentIonName,
                                 Transition.GetMassIndexText(tran.MassIndex),
                                 nodeTran.Mz,
                                 Transition.GetChargeIndicator(tran.Adduct));
        }

        public static string GetTitle(TransitionGroupDocNode nodeGroup)
        {
            if (nodeGroup == null)
                return string.Empty;

            var seq = nodeGroup.TransitionGroup.Peptide.Target.Sequence; // Not using Peptide.RawTextId, see comment below
            if (nodeGroup.TransitionGroup.IsCustomIon)
            {
                // Showing precursor m/z, so avoid showing ion masses as in DisplayName
                var customIon = nodeGroup.CustomMolecule;
                seq = customIon.Name ?? customIon.Formula;
            }
            string prefix = string.Empty;
            if (seq != null)
                prefix = seq + @" - ";
            
            return string.Format(@"{0}{1:F04}{2}{3}", prefix, nodeGroup.PrecursorMz,
                                 Transition.GetChargeIndicator(nodeGroup.TransitionGroup.PrecursorAdduct),
                                 nodeGroup.TransitionGroup.LabelTypeText);            
        }

        public static string GetTitle(PeptideDocNode nodePep)
        {
            if (nodePep == null)
            {
                return string.Empty;
            }
            return nodePep.ModifiedSequenceDisplay;
        }

        public override string Title
        {
            get
            {
                if (_isSummary)
                {
                    string title = Chromatogram.FilePath.GetSampleOrFileName();
                    var extractor = Chromatogram.Header.Extractor;
                    if (extractor == ChromExtractor.base_peak)
                        return string.Format(Resources.ChromGraphItem_Title__0____base_peak, title);
                    if (extractor == ChromExtractor.summed)
                        return string.Format(Resources.ChromGraphItem_Title__0____TIC, title);
                    return Chromatogram.GroupInfo.TextId ?? @"no summary text";
                }
                if (_step != 0)
                    return string.Format(Resources.ChromGraphItem_Title_Step__0_, _step);

                return GetTitle(TransitionGroupNode, TransitionNode);
            }
        }

        public override IPointList Points
        {
            get
            {
                return new PointPairList(_displayTimes, _intensities);
            }
        }

        public override void AddPreCurveAnnotations(MSGraphPane graphPane, Graphics g,
                                            MSPointList pointList, GraphObjList annotations)
        {
            if (Chromatogram == null)
                return;

            // Give priority to showing the best peak text object above all other annotations
            if (DragInfo != null || (!HideBest && TransitionChromInfo != null) || CurveAnnotation != null)
            {
                // Show text and arrow for the best peak
                double intensityBest = 0;
                if (_bestPeakTimeIndex != -1)
                {
                    ScaledRetentionTime timeBest = new ScaledRetentionTime(_measuredTimes[_bestPeakTimeIndex], _displayTimes[_bestPeakTimeIndex]);
                    float xBest = graphPane.XAxis.Scale.Transform(timeBest.DisplayTime);
                    intensityBest = _intensities[_bestPeakTimeIndex];
                    float yBest = graphPane.YAxis.Scale.Transform(intensityBest);

                    if (GraphChromatogram.ShowRT != ShowRTChrom.none || DragInfo != null)
                    {
                        // Best peak gets its own label to avoid curve overlap detection
                        double intensityLabel = graphPane.YAxis.Scale.ReverseTransform(yBest - 5);
                        float? massError = Settings.Default.ShowMassError && TransitionChromInfo != null
                                               ? TransitionChromInfo.MassError
                                               : null;
                        double dotProduct = _dotProducts != null ? _bestProduct : 0;

                        TextObj text;
                        if (CurveAnnotation != null)
                        {
                            // Darken peptide name a little so light colors stand out against the white background.
                            var color = FontSpec.FontColor;
                            if (!GraphInfo.IsSelected)
                                color = Color.FromArgb(color.R*7/10, color.G*7/10, color.B*7/10);
                            var fontSpec = new FontSpec(FontSpec) { FontColor = color, Angle = 90 };
                            if (GraphInfo.IsSelected)
                                fontSpec = new FontSpec(fontSpec) {IsBold = true, Size = fontSpec.Size + 2, IsAntiAlias = true};

                            // Display peptide name label using vertical text.
                            text = new TextObj(CurveAnnotation, timeBest.DisplayTime, intensityLabel,
                                CoordType.AxisXYScale, AlignH.Left, AlignV.Center)
                            {
                                ZOrder = ZOrder.A_InFront,
                                IsClippedToChartRect = true,
                                FontSpec = fontSpec,
                                Tag = new GraphObjTag(this, GraphObjType.best_peak, timeBest),
                            };
                        }
                        else
                        {
                            string label = FormatTimeLabel(timeBest.DisplayTime, massError, dotProduct, Chromatogram.GetIonMobilityFilter());

                            text = new TextObj(label, timeBest.DisplayTime, intensityLabel,
                                CoordType.AxisXYScale, AlignH.Center, AlignV.Bottom)
                            {
                                ZOrder = ZOrder.A_InFront,
                                IsClippedToChartRect = true,
                                FontSpec = FontSpec,
                                Tag = new GraphObjTag(this, GraphObjType.best_peak, timeBest),
                            };
                        }

                        annotations.Add(text);
                    }

                    // If showing multiple peptides, skip the best peak arrow indicator.
                    if (CurveAnnotation == null)
                    {
                        // Show the best peak arrow indicator
                        double timeArrow = graphPane.XAxis.Scale.ReverseTransform(xBest - 4);
                        double intensityArrow = graphPane.YAxis.Scale.ReverseTransform(yBest - 2);

                        ArrowObj arrow = new ArrowObj(COLOR_BEST_PEAK, 12f,
                            timeArrow, intensityArrow, timeArrow, intensityArrow)
                        {
                            Location = {CoordinateFrame = CoordType.AxisXYScale},
                            IsArrowHead = true,
                            IsClippedToChartRect = true,
                            ZOrder = ZOrder.A_InFront
                        };
                        annotations.Add(arrow);
                    }
                }

                // Show the best peak boundary lines
                if (CurveAnnotation == null)
                {
                    double startTime = 0, endTime = 0;
                    if (DragInfo != null)
                    {
                        startTime = DragInfo.StartTime.MeasuredTime;
                        endTime = DragInfo.EndTime.MeasuredTime;
                    }
                    else if (TransitionChromInfo != null)
                    {
                        var tranPeakInfo = TransitionChromInfo;
                        startTime = tranPeakInfo.StartRetentionTime;
                        endTime = tranPeakInfo.EndRetentionTime;
                    }
                    AddPeakBoundaries(graphPane, annotations, true,
                        ScaleRetentionTime(startTime), ScaleRetentionTime(endTime), intensityBest);
                }
                if (Chromatogram.BestPeakIndex >= 0)
                {
                    // Only shade peak when user modified. Otherwise, shading can be added when an entire
                    // precursor was force integrated because of another precursor (e.g. heavy) since that
                    // leads to an empty peak, which will not match the best peak.
                    if (Settings.Default.ShowOriginalPeak && TransitionChromInfo != null && TransitionChromInfo.IsUserModified)
                    {
                        var bestPeak = Chromatogram.GetPeak(Chromatogram.BestPeakIndex);
                        if (bestPeak.StartTime != TransitionChromInfo.StartRetentionTime ||
                            bestPeak.EndTime != TransitionChromInfo.EndRetentionTime)
                        {
                            AddOriginalPeakAnnotation(bestPeak, annotations, graphPane);
                        }
                    }
                }
            }
            if (_displayRawTimes.HasValue)
            {
                AddPeakRawTimes(graphPane, annotations,
                    ScaleRetentionTime(_displayRawTimes.Value.StartBound),
                    ScaleRetentionTime(_displayRawTimes.Value.EndBound),
                    Chromatogram);
            }
        }

        private void AddOriginalPeakAnnotation(ChromPeak bestPeak, GraphObjList annotations, GraphPane graphPane)
        {
            var start = ScaleRetentionTime(bestPeak.StartTime);
            var end = ScaleRetentionTime(bestPeak.EndTime);
            var width = end.DisplayTime - start.DisplayTime;
            var height = graphPane.YAxis.Scale.Max;
            var originalPeakShadingBox = new BoxObj(start.DisplayTime, graphPane.YAxis.Scale.Max, width, height)
            {
                Fill = new Fill(COLOR_ORIGINAL_PEAK_SHADE),
                ZOrder = ZOrder.F_BehindGrid,
                Border = new Border { IsVisible = false },
                IsClippedToChartRect = true,
                Tag = new GraphObjTag(this, GraphObjType.original_peak_shading, start, end)
            };
            annotations.Add(originalPeakShadingBox);
        }

        public override void AddAnnotations(MSGraphPane graphPane, Graphics g,
                                            MSPointList pointList, GraphObjList annotations)
        {
            if (Chromatogram == null)
                return;


            // Calculate maximum y for potential retention time indicators
            PointF ptTop = new PointF(0, graphPane.Chart.Rect.Top);

            if (GraphChromatogram.ShowRT != ShowRTChrom.none)
            {
                if (RetentionMsMs != null)
                {
                    foreach (double retentionTime in RetentionMsMs)
                    {
                        Color color = COLOR_MSMSID_TIME;
                        if (SelectedRetentionMsMs.HasValue && Equals((float) retentionTime, (float) SelectedRetentionMsMs))
                        {
                            color = ColorScheme.ChromGraphItemSelected;
                        }
                        AddRetentionTimeAnnotation(graphPane, g, annotations, ptTop,
                            Resources.ChromGraphItem_AddAnnotations_ID, GraphObjType.ms_ms_id, color,
                            ScaleRetentionTime(retentionTime));
                    }
                }
                if (MidasRetentionMsMs != null)
                {
                    foreach (var retentionTime in MidasRetentionMsMs)
                    {
                        var color = SelectedRetentionMsMs.HasValue && Equals((float) retentionTime, (float) SelectedRetentionMsMs)
                            ? ColorScheme.ChromGraphItemSelected
                            : COLOR_MSMSID_TIME;
                        AddRetentionTimeAnnotation(graphPane, g, annotations, ptTop, string.Empty, GraphObjType.midas_spectrum, color, ScaleRetentionTime(retentionTime));
                    }
                }
                if (AlignedRetentionMsMs != null)
                {
                    foreach (var time in AlignedRetentionMsMs)
                    {
                        var scaledTime = ScaleRetentionTime(time);
                        var line = new LineObj(COLOR_ALIGNED_MSMSID_TIME, scaledTime.DisplayTime, 0,
                                               scaledTime.DisplayTime, 1)
                        {
                            ZOrder = ZOrder.F_BehindGrid,
                            Location = { CoordinateFrame = CoordType.XScaleYChartFraction },
                            IsClippedToChartRect = true,
                            Tag = new GraphObjTag(this, GraphObjType.aligned_ms_id, scaledTime),
                        };
                        annotations.Add(line);
                    }
                }
                if (UnalignedRetentionMsMs != null)
                {
                    foreach (var time in UnalignedRetentionMsMs)
                    {
                        var scaledTime = ScaleRetentionTime(time);
                        var line = new LineObj(COLOR_UNALIGNED_MSMSID_TIME, scaledTime.DisplayTime, 0,
                                               scaledTime.DisplayTime, 1)
                        {
                            ZOrder = ZOrder.F_BehindGrid,
                            Location = { CoordinateFrame = CoordType.XScaleYChartFraction },
                            IsClippedToChartRect = true,
                            Tag = new GraphObjTag(this, GraphObjType.unaligned_ms_id, scaledTime),
                        };
                        annotations.Add(line);
                    }
                }
            }

            // If explicit retention time is in use, show that instead of predicted since it overrides
            if (RetentionExplicit != null)
            {
                var time = RetentionExplicit.RetentionTime;
                if (GraphChromatogram.ShowRT != ShowRTChrom.none)
                {
                    // Create temporary label to calculate positions
                    AddRetentionTimeAnnotation(graphPane,
                        g,
                        annotations,
                        ptTop,
                        Resources.ChromGraphItem_AddAnnotations_Explicit,
                        GraphObjType.predicted_rt_window,
                        COLOR_RETENTION_TIME,
                        ScaleRetentionTime(time));
                }
                // Draw background for retention time window
                if ((RetentionExplicit.RetentionTimeWindow??0) > 0.0)
                {
                    var halfwin = (RetentionExplicit.RetentionTimeWindow??0) / 2.0;
                    double x1 = ScaleRetentionTime(time - halfwin).DisplayTime;
                    double x2 = ScaleRetentionTime(time + halfwin).DisplayTime;
                    BoxObj box = new BoxObj(x1, 0, x2 - x1, 1,
                        COLOR_RETENTION_WINDOW, COLOR_RETENTION_WINDOW)
                    {
                        Location = { CoordinateFrame = CoordType.XScaleYChartFraction },
                        IsClippedToChartRect = true,
                        ZOrder = ZOrder.F_BehindGrid
                    };
                    annotations.Add(box);
                }
            }

            // Draw retention time indicator, if set
            else if (RetentionPrediction.HasValue)
            {
                double time = RetentionPrediction.Value;

                // Create temporary label to calculate positions
                if (GraphChromatogram.ShowRT != ShowRTChrom.none)
                {
                    AddRetentionTimeAnnotation(graphPane,
                                               g,
                                               annotations,
                                               ptTop,
                                               Resources.ChromGraphItem_AddAnnotations_Predicted,
                                               GraphObjType.predicted_rt_window,
                                               COLOR_RETENTION_TIME,
                                               ScaleRetentionTime(time));
                }

                // Draw background for retention time window
                if (RetentionWindow > 0)
                {
                    double x1 = ScaleRetentionTime(time - RetentionWindow/2).DisplayTime;
                    double x2 = ScaleRetentionTime(time + RetentionWindow/2).DisplayTime;
                    BoxObj box = new BoxObj(x1, 0, x2-x1, 1,
                                            COLOR_RETENTION_WINDOW, COLOR_RETENTION_WINDOW)
                                     {
                                         Location = { CoordinateFrame = CoordType.XScaleYChartFraction },
                                         IsClippedToChartRect = true,
                                         ZOrder = ZOrder.F_BehindGrid
                                     };
                    annotations.Add(box);
                }
            }

            for (int i = 0, len = Chromatogram.NumPeaks; i < len; i++)
            {
                if (_arrayLabelIndexes[i] == -1)
                    continue;

                double maxIntensity = _intensities[_arrayLabelIndexes[i]];

                // Show peak extent indicators, if they are far enough apart
                ChromPeak peak = Chromatogram.GetPeak(i);
                AddPeakBoundaries(graphPane, annotations, false,
                                  ScaleRetentionTime(peak.StartTime), ScaleRetentionTime(peak.EndTime), maxIntensity);
            }
        }

        private void AddRetentionTimeAnnotation(MSGraphPane graphPane, Graphics g, GraphObjList annotations,
            PointF ptTop, string title, GraphObjType graphObjType, Color color, ScaledRetentionTime retentionTime)
        {
            // ReSharper disable LocalizableElement
            string label = string.Format("{0}\n{1:F01}", title, retentionTime.DisplayTime);
            // ReSharper restore LocalizableElement
            FontSpec fontLabel = CreateFontSpec(color, _fontSpec.Size);
            SizeF sizeLabel = fontLabel.MeasureString(g, label, graphPane.CalcScaleFactor());
            PointF realTopPoint = ptTop;
            ptTop = new PointF(0, ptTop.Y + sizeLabel.Height + 15);
            float chartHeightWithLabel = graphPane.Chart.Rect.Height + sizeLabel.Height + 15;
            double intensityChartFraction = (ptTop.Y - realTopPoint.Y) / chartHeightWithLabel;

            LineObj stick = new LineObj(color, retentionTime.DisplayTime, intensityChartFraction, retentionTime.DisplayTime, 1)
                                {
                                    IsClippedToChartRect = true,
                                    Location = { CoordinateFrame = CoordType.XScaleYChartFraction },
                                    ZOrder = ZOrder.E_BehindCurves,
                                    Line = { Width = 1 },
                                    Tag = new GraphObjTag(this, graphObjType, retentionTime),
                                };
            annotations.Add(stick);

            ptTop = new PointF(0, ptTop.Y - 5);
            intensityChartFraction = (ptTop.Y - realTopPoint.Y) / chartHeightWithLabel;
            TextObj text = new TextObj(label, retentionTime.DisplayTime, intensityChartFraction,
                                       CoordType.XScaleYChartFraction, AlignH.Center, AlignV.Bottom)
                               {
                                   IsClippedToChartRect = true,
                                   ZOrder = ZOrder.E_BehindCurves,
                                   FontSpec = CreateFontSpec(color, _fontSpec.Size),
                                   Tag = new GraphObjTag(this, graphObjType, retentionTime),
                               };
            annotations.Add(text);
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
            while (iLastStart < _measuredTimes.Length && _measuredTimes[iLastStart] < startTime)
                iLastStart++;
            // Search forward for the maximum intensity until the end of the peak is reached
            int maxIndex = -1;
            double maxIntensity = 0;
            for (int iPoint = iLastStart; iPoint < _measuredTimes.Length && _measuredTimes[iPoint] < endTime; iPoint++)
            {
                if (_intensities[iPoint] > maxIntensity)
                {
                    maxIntensity = _intensities[iPoint];
                    maxIndex = iPoint;
                }
            }
            return maxIndex;
        }

        private void AddPeakRawTimes(GraphPane graphPane, ICollection<GraphObj> annotations,
            ScaledRetentionTime startTime, ScaledRetentionTime endTime, ChromatogramInfo info)
        {
            var hasTimes = info.RawTimes != null && info.RawTimes.Any(); // has measured points

            var scaledHeight = graphPane.YAxis.Scale.Max / 20; // 5% of graph pane height
            var rawtimes = new List<double>();

            if (hasTimes)
            {
                rawtimes.AddRange(GetRawTimes(startTime, endTime, info));
                if (rawtimes.Count == 0)
                    return;
                foreach (var time in rawtimes)
                {
                    LineObj stick = new LineObj(time, scaledHeight, time, 0)
                    {
                        IsClippedToChartRect = true,
                        Location = { CoordinateFrame = CoordType.AxisXYScale },
                        ZOrder = ZOrder.A_InFront,
                        Line = { Width = 1, Style = DashStyle.Dash, Color = ColorScheme.ChromGraphItemSelected },
                        Tag = new GraphObjTag(this, GraphObjType.raw_time, new ScaledRetentionTime(time)),
                    };
                    annotations.Add(stick);
                }
            }
            
            var countTxt = hasTimes ? @" " + rawtimes.Count : @" ?";
            var isBold = !hasTimes; // Question mark if no times exist is visually clearer if bold
            TextObj pointCount = new TextObj(countTxt, endTime.DisplayTime, scaledHeight)
            {
                FontSpec = new FontSpec(FontSpec.Family, FontSpec.Size, ColorScheme.ChromGraphItemSelected, isBold, false, false)
                {
                    Border = new Border { IsVisible = false },
                    Fill = FontSpec.Fill
                },
                Location =
                {
                    AlignH = AlignH.Left,
                    AlignV = AlignV.Bottom
                }

            };
            annotations.Add(pointCount);
        }

        private IEnumerable<double> GetRawTimes(ScaledRetentionTime startTime, ScaledRetentionTime endTime, ChromatogramInfo info)
        {
            double end = endTime.DisplayTime;
            double start = startTime.DisplayTime;
            var times = info.RawTimes;
            if (times != null)
            {
                for (int j = 0; j < times.Count; j++)
                {
                    if (start > times[j])
                        continue;
                    if (end < times[j])
                        break;
                    yield return times[j];
                }
            }
        }

        public int RawTimesCount
        {
            get
            {
                if (_displayRawTimes.HasValue && Chromatogram != null)
                {
                    return GetRawTimes(ScaleRetentionTime(_displayRawTimes.Value.StartBound),
                        ScaleRetentionTime(_displayRawTimes.Value.EndBound),
                        Chromatogram).Count();
                }
                return 0;
            }
        }

        private void AddPeakBoundaries(GraphPane graphPane, ICollection<GraphObj> annotations,
                                       bool best, ScaledRetentionTime startTime, ScaledRetentionTime endTime, double maxIntensity)
        {
            // Only show boundaries for dragging when boundaries turned off
            if (!Settings.Default.ShowPeakBoundaries && (!best || DragInfo == null))
                return;
            float xStart = graphPane.XAxis.Scale.Transform(startTime.DisplayTime);
            float xEnd = graphPane.XAxis.Scale.Transform(endTime.DisplayTime);
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
            // Summary graphs show only the best peak boundaries
            else if (_isSummary)
            {
                return;
            }

            Color colorBoundaries = (best ? COLOR_BOUNDARIES_BEST : COLOR_BOUNDARIES);
            GraphObjType graphObjType = best ? GraphObjType.best_peak : GraphObjType.peak;

            // Make sure to get maximum intensity within the peak range,
            // as this is not guaranteed to be the center of the peak
            LineObj stickStart = new LineObj(colorBoundaries, startTime.DisplayTime, maxIntensity, startTime.DisplayTime, 0)
                                     {
                                         IsClippedToChartRect = true,
                                         Location = { CoordinateFrame = CoordType.AxisXYScale },
                                         ZOrder = ZOrder.B_BehindLegend,
                                         Line = { Width = 1, Style = DashStyle.Dash },
                                         Tag = new GraphObjTag(this, graphObjType, startTime),
                                     };
            annotations.Add(stickStart);
            LineObj stickEnd = new LineObj(colorBoundaries, endTime.DisplayTime, maxIntensity, endTime.DisplayTime, 0)
                                   {
                                       IsClippedToChartRect = true,
                                       Location = { CoordinateFrame = CoordType.AxisXYScale },
                                       ZOrder = ZOrder.B_BehindLegend,
                                       Line = { Width = 1, Style = DashStyle.Dash },
                                       Tag = new GraphObjTag(this, graphObjType, endTime),
                                   };
            annotations.Add(stickEnd);
        }

        public override PointAnnotation AnnotatePoint(PointPair point)
        {
            var showRT = GraphChromatogram.ShowRT;
            int timeIndex = Array.BinarySearch(_displayTimes, point.X);
            if (showRT == ShowRTChrom.all||
                    (showRT == ShowRTChrom.threshold && Settings.Default.ShowRetentionTimesThreshold <= point.Y))
            {
                int indexPeak;
                if (_annotatedTimes.TryGetValue(timeIndex, out indexPeak))
                {
                    double dotProduct = _dotProducts != null ? _dotProducts[indexPeak] : 0;
                    float? massError = null;
                    if (Settings.Default.ShowMassError)
                        massError = Chromatogram.GetPeak(indexPeak).MassError;
                    string label = FormatTimeLabel(point.X, massError, dotProduct, IonMobilityFilter.EMPTY);
                    return new PointAnnotation(label, FontSpec);
                }
            }

            return null;
        }

        public string FormatTimeLabel(double time, float? massError, double dotProduct, IonMobilityFilter ionMobilityfilter)
        {
            // ReSharper disable LocalizableElement
            var lines = new List<string> {string.Format($"{{0:F0{Settings.Default.ChromatogramDisplayRTDigits}}}", time)};
            if (massError.HasValue && !_isSummary)
                lines.Add(string.Format("{0}{1} ppm", (massError.Value > 0 ? "+" : string.Empty), massError.Value));
            if (dotProduct != 0)
                lines.Add(string.Format("({0} {1:F02})", _isFullScanMs ? "idotp" : "dotp", dotProduct));

            // Ion mobility values
            if (ionMobilityfilter.IonMobility.HasValue && !_isSummary && 
                ionMobilityfilter.IonMobilityUnits != eIonMobilityUnits.waters_sonar && // SONAR data isn't really ion mobility, it just uses some of the same filter mechanisms
                (Settings.Default.ShowCollisionCrossSection || Settings.Default.ShowIonMobility))
            {
                if (Settings.Default.ShowCollisionCrossSection && 
                    ionMobilityfilter.IonMobilityUnits != eIonMobilityUnits.compensation_V) // CCS isn't measurable with FAIMS
                {
                    var ccsString = FormatCollisionCrossSectionValue(ionMobilityfilter);
                    lines.Add(ccsString);
                }
                if (Settings.Default.ShowIonMobility)
                {
                    var imString = FormatIonMobilityValue(ionMobilityfilter);
                    lines.Add(string.Format("IM {0}", imString));
                }
            }

            // N.B.you might expect use of TextUtil.LineSeparate() here, but this string is parsed
            // elsewhere with the expectation of \n as separator rather than \r\n
            return string.Join("\n", lines); 

            // ReSharper restore LocalizableElement
        }

        public static string FormatIonMobilityValue(IonMobilityFilter ionMobilityFilter)
        {
            var imString = ionMobilityFilter.IonMobility.HasValue
                ? string.Format(@"{0:F02} {1}",
                    ionMobilityFilter.IonMobility.Mobility, 
                    IonMobilityValue.GetUnitsString(ionMobilityFilter.IonMobilityUnits).Replace(@"^2", @"²")) // Make "Vs/cm^2" into "Vs/cm²" to agree with CCS "Å²"
                : @"IM unknown"; // Should never happen
            return imString;
        }

        public static string FormatCollisionCrossSectionValue(IonMobilityFilter ionMobilityFilter)
        {
            var ccsString = ionMobilityFilter.CollisionalCrossSectionSqA.HasValue
                ? string.Format(@"CCS {0:F02} Å²", ionMobilityFilter.CollisionalCrossSectionSqA.Value)
                : @"CCS unknown"; // Should never happen, except for very old data
            return ccsString;
        }

        public IdentityPath FindIdentityPath(TextObj label)
        {
            var tag = label.Tag as GraphObjTag;
            return tag != null ? tag.ChromGraphItem.IdPath : null;
        }

        public ScaledRetentionTime FindPeakRetentionTime(TextObj label)
        {
            var tag = label.Tag as GraphObjTag;
            if (null != tag || !ReferenceEquals(FontSpec, label.FontSpec))
            {
                return ScaledRetentionTime.ZERO;
            }
            return FindPeakRetentionTime(label.Location.X);
        }

        public ScaledRetentionTime FindPeakRetentionTime(double time)
        {
            // Search for a time that corresponds with a label
            int closestLabelIndex = -1;
            double closestLabelDeltaTime = Double.MaxValue;
            for (int i = 0; i < _arrayLabelIndexes.Length; i++)
            {
                int iLabel = _arrayLabelIndexes[i];
                if (iLabel == -1)
                    continue;
                double deltaTime = Math.Abs(time - _displayTimes[iLabel]);
                if (deltaTime < 0.15 && deltaTime < closestLabelDeltaTime)
                {
                    closestLabelIndex = i;
                    closestLabelDeltaTime = deltaTime;
                }
            }
            if (closestLabelIndex == -1)
                return ScaledRetentionTime.ZERO;

            var peak = Chromatogram.GetPeak(closestLabelIndex);
            float rt = peak.RetentionTime;
            int iTime = Array.BinarySearch(_measuredTimes, rt);
            if (iTime < 0)
            {
                iTime = ~iTime;
                if (iTime > _measuredTimes.Length - 1 ||
                    (iTime > 0 && Math.Abs(rt - _measuredTimes[iTime]) > Math.Abs(rt - _measuredTimes[iTime - 1])))
                {
                    iTime--;
                }
            }
            return new ScaledRetentionTime(rt, _displayTimes[iTime]);
        }

        public ScaledRetentionTime FindSpectrumRetentionTime(GraphObj graphObj)
        {
            var tag = graphObj.Tag as GraphObjTag;
            if (null == tag || !ReferenceEquals(this, tag.ChromGraphItem) ||
                (GraphObjType.ms_ms_id != tag.GraphObjType && GraphObjType.midas_spectrum != tag.GraphObjType))
            {
                return ScaledRetentionTime.ZERO;
            }
            return tag.RetentionTime;
        }

        public ScaledRetentionTime GetNearestBestPeakBoundary(double displayTime)
        {
            var tranPeakInfo = TransitionChromInfo;
            if (tranPeakInfo == null)
            {
                return ScaledRetentionTime.ZERO;
            }
            var peakStartTime = ScaleRetentionTime(tranPeakInfo.StartRetentionTime);
            var peakEndTime = ScaleRetentionTime(tranPeakInfo.EndRetentionTime);
            double deltaStart = Math.Abs(peakStartTime.DisplayTime - displayTime);
            double deltaEnd = Math.Abs(peakEndTime.DisplayTime - displayTime);
            return deltaStart < deltaEnd ? peakStartTime : peakEndTime;
        }

        public double MeasuredTimeToDisplayTime(double time)
        {
            if (TimeRegressionFunction == null)
            {
                return time;
            }
            return TimeRegressionFunction.GetY(time);
        }

        public ScaledRetentionTime GetValidPeakBoundaryTime(double displayTime)
        {
            double measuredTime = TimeRegressionFunction == null
                ? displayTime
                : TimeRegressionFunction.GetX(displayTime);
            var chromatogramInfo = Chromatogram;
            if (chromatogramInfo.TimeIntervals != null)
            {
                return ScaleRetentionTime(measuredTime);
            }

            var interpolatedTimeIntensities = chromatogramInfo.GetInterpolatedTimeIntensities();
            int index = interpolatedTimeIntensities.IndexOfNearestTime((float)measuredTime);
            return ScaleRetentionTime(interpolatedTimeIntensities.Times[index]);
        }

        public int GetNearestMeasuredIndex(double measuredTime)
        {
            return NearestIndex(_measuredTimes, measuredTime);
        }

        private static int NearestIndex(double[] sortedArray, double value)
        {
            int index = Array.BinarySearch(sortedArray, value);
            if (index < 0)
            {
                index = ~index;
                if (index == sortedArray.Length || (index > 0 && sortedArray[index] - value > value - sortedArray[index - 1]))
                {
                    index--;
                }
            }
            return index;
        }

        public enum GraphObjType
        {
// ReSharper disable UnusedMember.Local
            invalid,
// ReSharper restore UnusedMember.Local
            ms_ms_id,
            midas_spectrum,
            predicted_rt_window,
            aligned_ms_id,
            unaligned_ms_id,
            best_peak,
            raw_time,
            peak,
            original_peak_shading
        }

        public class GraphObjTag
        {
            public GraphObjTag(ChromGraphItem chromGraphItem, GraphObjType graphObjType, ScaledRetentionTime retentionTime)
            {
                ChromGraphItem = chromGraphItem;
                GraphObjType = graphObjType;
                RetentionTime = retentionTime;
                
            }

            public GraphObjTag(ChromGraphItem chromGraphItem, GraphObjType graphObjType, ScaledRetentionTime start, ScaledRetentionTime end)
            {
                ChromGraphItem = chromGraphItem;
                GraphObjType = graphObjType;
                StartTime = start;
                EndTime = end;
            }

            public ChromGraphItem ChromGraphItem { get; private set; }
            public GraphObjType GraphObjType { get; private set; }
            public ScaledRetentionTime RetentionTime { get; private set; }
            public ScaledRetentionTime StartTime { get; private set; }
            public ScaledRetentionTime EndTime { get; private set; }

            public override string ToString()
            {
                return string.Format(@"{0}:{1}", GraphObjType, RetentionTime);
            }
        }
    }

    public sealed class FailedChromGraphItem : NoDataChromGraphItem
    {
        public FailedChromGraphItem(TransitionGroupDocNode nodeGroup, Exception x)
            : base(string.Format(Resources.FailedChromGraphItem_FailedChromGraphItem__0__load_failed__1__, ChromGraphItem.GetTitle(nodeGroup), x.Message))
        {            
        }
    }

    public sealed class NotFoundChromGraphItem : NoDataChromGraphItem
    {
        public NotFoundChromGraphItem(TransitionDocNode nodeTran)
            : base(string.Format(Resources.NotFoundChromGraphItem_NotFoundChromGraphItem__0__not_found, ChromGraphItem.GetTitle(nodeTran)))
        {
        }

        public NotFoundChromGraphItem(TransitionGroupDocNode nodeGroup)
            : base(string.Format(Resources.NotFoundChromGraphItem_NotFoundChromGraphItem__0__not_found, ChromGraphItem.GetTitle(nodeGroup)))
        {
        }
    }

    public sealed class UnavailableChromGraphItem : NoDataChromGraphItem
    {
        public UnavailableChromGraphItem(string message) : base(message ?? Resources.UnavailableChromGraphItem_UnavailableChromGraphItem_Chromatogram_information_unavailable)
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

        public override void AddPreCurveAnnotations(MSGraphPane graphPane, Graphics g, MSPointList pointList, GraphObjList annotations)
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
        public abstract void AddPreCurveAnnotations(MSGraphPane graphPane, Graphics g,
                                            MSPointList pointList, GraphObjList annotations);
        public abstract IPointList Points { get; }

        public virtual Color Color { get; set; }

        public float LineWidth { get; set; }

        public virtual void CustomizeCurve(CurveItem curveItem)
        {
            // Do nothing by default
        }

        public MSGraphItemType GraphItemType
        {
            get { return MSGraphItemType.chromatogram; }
        }

        public MSGraphItemDrawMethod GraphItemDrawMethod
        {
            get { return MSGraphItemDrawMethod.line; }
        }

        public void CustomizeYAxis(Axis axis)
        {
            CustomizeAxis(axis, Resources.AbstractChromGraphItem_CustomizeYAxis_Intensity);
        }

        public void CustomizeXAxis(Axis axis)
        {
            CustomizeAxis(axis, Resources.AbstractChromGraphItem_CustomizeXAxis_Retention_Time);
        }

        private static void CustomizeAxis(Axis axis, string title)
        {
            axis.Title.FontSpec.Family = @"Arial";
            axis.Title.FontSpec.Size = 14;
            axis.Color = axis.Title.FontSpec.FontColor = Color.Black;
            axis.Title.FontSpec.Border.IsVisible = false;
            axis.Title.Text = title;
        }
    }

    public class FullScanInfo
    {
        public ChromatogramInfo ChromInfo;
        public string ScanName;
    }
}
