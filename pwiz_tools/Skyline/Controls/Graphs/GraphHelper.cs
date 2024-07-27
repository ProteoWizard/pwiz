/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using pwiz.Skyline.Util;
using ZedGraph;
using pwiz.MSGraph;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Results.Spectra;
using pwiz.Skyline.Model.RetentionTimes;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Controls.Graphs
{
    public class GraphHelper
    {
        private DisplayState _displayState;
        private bool _zoomLocked;

        public const string SCIENTIFIC_NOTATION_FORMAT_STRING = "0.0#####e0";

        public GraphHelper(MSGraphControl msGraphControl)
        {
            GraphControl = msGraphControl;
            _displayState = new ErrorDisplayState();
        }

        public static GraphHelper Attach(MSGraphControl msGraphControl)
        {
            GraphHelper graphHelper = new GraphHelper(msGraphControl);
            msGraphControl.MasterPane.Border.IsVisible = false;
            msGraphControl.GraphPane.Border.IsVisible = false;
            msGraphControl.GraphPane.AllowCurveOverlap = true;
            return graphHelper;
        }

        public MSGraphControl GraphControl { get; private set; }
        public IEnumerable<MSGraphPane> GraphPanes { get { return GraphControl.MasterPane.PaneList.OfType<MSGraphPane>(); } }
        public PaneKey GetPaneKey(GraphPane graphPane) { return _displayState.GraphPaneKeys.FirstOrDefault(paneKey => ReferenceEquals(GetGraphPane(paneKey), graphPane)); }
        public IEnumerable<KeyValuePair<PaneKey, ChromGraphItem>> ListPrimaryGraphItems()
        {
            var chromDisplayState = _displayState as ChromDisplayState;
            if (null == chromDisplayState)
            {
                return new KeyValuePair<PaneKey, ChromGraphItem>[0];
            }
            return chromDisplayState.ChromGraphItems.Where(kvp => kvp.Value.Chromatogram != null && kvp.Value.TransitionGroupNode != null)
                                                    .ToLookup(kvp => kvp.Key)
                                                    .Select(grouping => grouping.Last());
        }

        public void LockZoom()
        {
            _zoomLocked = true;
        }

        public void UnlockZoom()
        {
            _zoomLocked = false;
        }

        private void SetDisplayState(DisplayState newDisplayState)
        {
            if (_zoomLocked && _displayState.GetType() == newDisplayState.GetType())
            {
                foreach (var pane in GraphControl.MasterPane.PaneList)
                {
                    pane.CurveList.Clear();
                    pane.GraphObjList.Clear();
                }
                _displayState.ZoomStateValid = true;
                return;
            }
            while (GraphControl.MasterPane.PaneList.Count > 1)
            {
                // Remove all but the first graph pane so that the zoom state stack is preserved.
                GraphControl.MasterPane.PaneList.RemoveRange(1, GraphControl.MasterPane.PaneList.Count - 1);
                using (var graphics = GraphControl.CreateGraphics())
                {
                    GraphControl.MasterPane.SetLayout(graphics, PaneLayout.SingleColumn);
                }
            }
            GraphControl.GraphPane.CurveList.Clear();
            GraphControl.GraphPane.GraphObjList.Clear();
            newDisplayState.ZoomStateValid = newDisplayState.CanUseZoomStateFrom(_displayState);
            newDisplayState.ApplySettingsToGraphPane(GraphControl.GraphPane);
            _displayState = newDisplayState;
        }

        public void ResetForChromatograms(IEnumerable<TransitionGroup> transitionGroups,
            bool forceLegendDisplay = false)
        {
            ResetForChromatograms(TransformChrom.raw, transitionGroups, false, forceLegendDisplay);
        }

        public void ResetForChromatograms(TransformChrom transformChrom, IEnumerable<TransitionGroup> transitionGroups, bool proteinSelected, bool forceLegendDisplay)
        {
            SetDisplayState(new ChromDisplayState(Settings.Default, transformChrom, transitionGroups, proteinSelected, forceLegendDisplay));
        }

        public void FinishedAddingChromatograms(double bestPeakStartTime, double bestPeakEndTime, bool forceZoom)
        {
            var retentionTimeValues = new RetentionTimeValues((bestPeakStartTime + bestPeakEndTime) / 2, bestPeakStartTime, bestPeakEndTime, 0, null);
            FinishedAddingChromatograms(new[] { retentionTimeValues }, forceZoom);
        }

        public void FinishedAddingChromatograms(IEnumerable<RetentionTimeValues> bestPeaks, bool forceZoom)
        {
            var bestPeakList = bestPeaks.Where(peak => null != peak).ToList();
            if (!_zoomLocked)
            {
                if (forceZoom || !_displayState.ZoomStateValid)
                {
                    var chromDisplayState = _displayState as ChromDisplayState;
                    if (chromDisplayState != null)
                    {
                        AutoZoomChromatograms(bestPeakList);
                    }
                }
            }
            using (var graphics = GraphControl.CreateGraphics())
            {
                foreach (MSGraphPane graphPane in GraphControl.MasterPane.PaneList)
                {
                    // This sets the scale, but also gets point annotations.  So, it
                    // needs to be called every time, but only once for efficiency.
                    graphPane.SetScale(graphics);
                }
            }
            GraphControl.AxisChange();
            GraphControl.Invalidate();
        }

        public void ResetForSpectrum(IEnumerable<TransitionGroup> transitionGroups)
        {
            SetDisplayState(new SpectrumDisplayState(Settings.Default, transitionGroups));
        }

        private void AutoZoomChromatograms(IList<RetentionTimeValues> bestPeaks)
        {
            var chromDisplayState = (ChromDisplayState) _displayState;
            if (chromDisplayState.ZoomStateValid)
            {
                return;
            }

            var autoZoom = chromDisplayState.AutoZoomChrom;
            if (!bestPeaks.Any())
            {
                if (autoZoom == AutoZoomChrom.both)
                {
                    autoZoom = AutoZoomChrom.window;
                }
                if (autoZoom == AutoZoomChrom.peak)
                {
                    autoZoom = AutoZoomChrom.none;
                }
            }
            var chromGraphItem = GetRetentionTimeGraphItem(chromDisplayState);
            double? predictedRT = chromGraphItem?.RetentionPrediction;
            double? windowHalf = chromGraphItem?.RetentionWindow * 2 / 3;
            if (!predictedRT.HasValue)
            {
                if (autoZoom == AutoZoomChrom.both)
                {
                    autoZoom = AutoZoomChrom.peak;
                }

                if (autoZoom == AutoZoomChrom.window)
                {
                    autoZoom = AutoZoomChrom.none;
                }
            }
            switch (autoZoom)
            {
                case AutoZoomChrom.none:
                    foreach (var graphPane in GraphPanes)
                    {
                        // If no auto-zooming, make sure the X-axis auto-scales
                        // Setting these cancels all zoom and pan, even if they
                        // are already set.  So, check before changing.
                        graphPane.XAxis.Scale.MinAuto = true;
                        graphPane.XAxis.Scale.MaxAuto = true;
                    }
                    break;
                case AutoZoomChrom.peak:
                    var firstPeak = bestPeaks.OrderBy(peak => peak.StartRetentionTime).FirstOrDefault();
                    var lastPeak = bestPeaks.OrderByDescending(peak => peak.EndRetentionTime).FirstOrDefault();
                    if (firstPeak != null && lastPeak != null)
                    {
                        ZoomToPeaks(firstPeak, lastPeak);
                    }
                    break;
                case AutoZoomChrom.window:
                    ZoomXAxis((predictedRT - windowHalf).Value, (predictedRT + windowHalf).Value);
                    break;
                case AutoZoomChrom.both:
                    double start = bestPeaks.Min(peak => peak.StartRetentionTime);
                    double end = bestPeaks.Max(peak => peak.EndRetentionTime);
                    // Make sure the peak has enough room to display, since it may be
                    // much narrower than the retention time window.
                    start -= windowHalf.Value / 8;
                    end += windowHalf.Value / 8;
                    start = Math.Min(start, (predictedRT - windowHalf).Value);
                    end = Math.Max(end, (predictedRT + windowHalf).Value);
                    ZoomXAxis(start, end);
                    break;
            }
            foreach (var graphPane in GraphPanes)
            {
                if (chromDisplayState.MinIntensity == 0)
                {
                    graphPane.YAxis.Scale.MinAuto = true;
                    graphPane.LockYAxisAtZero = !chromDisplayState.TransformChrom.IsDerivative();
                }
                else
                {
                    graphPane.LockYAxisMinAtZero = false;
                    graphPane.YAxis.Scale.MinAuto = false;
                    graphPane.YAxis.Scale.Min = chromDisplayState.MinIntensity;
                }
                if (chromDisplayState.MaxIntensity == 0)
                    graphPane.YAxis.Scale.MaxAuto = true;
                else
                {
                    graphPane.YAxis.Scale.MaxAuto = false;
                    graphPane.YAxis.Scale.Max = chromDisplayState.MaxIntensity;
                }
            }
        }

        private static ChromGraphItem GetRetentionTimeGraphItem(ChromDisplayState chromDisplayState)
        {
            return chromDisplayState.ChromGraphItems.Select(p => p.Value)
                                                    .FirstOrDefault(g => g.RetentionWindow > 0);
        }

        public void ZoomSpectrumToSettings(SrmDocument document, TransitionGroupDocNode nodeGroup)
        {
            var spectrumDisplayState = _displayState as SpectrumDisplayState;
            if (null == spectrumDisplayState || spectrumDisplayState.ZoomStateValid)
            {
                return;
            }
            var axis = GraphControl.GraphPane.XAxis;
            var instrument = document.Settings.TransitionSettings.Instrument;
            if (!instrument.IsDynamicMin || nodeGroup == null)
                axis.Scale.Min = instrument.MinMz;
            else
                axis.Scale.Min = instrument.GetMinMz(nodeGroup.PrecursorMz);
            axis.Scale.MinAuto = false;
            axis.Scale.Max = instrument.MaxMz;
            axis.Scale.MaxAuto = false;
            GraphControl.Invalidate();
        }

        private void ZoomXAxis(double min, double max)
        {
            foreach (var graphPaneKeyItem in ListPrimaryGraphItems())
            {
                ScaledRetentionTime scaledMin = graphPaneKeyItem.Value.ScaleRetentionTime(min);
                ScaledRetentionTime scaledMax = graphPaneKeyItem.Value.ScaleRetentionTime(max);
                var graphPane = _displayState.GetGraphPane(GraphControl, graphPaneKeyItem.Key);
                var axis = graphPane.XAxis;
                axis.Scale.Min = scaledMin.DisplayTime;
                axis.Scale.MinAuto = false;
                axis.Scale.Max = scaledMax.DisplayTime;
                axis.Scale.MaxAuto = false;
            }
        }

        public CurveItem AddChromatogram(PaneKey paneKey, ChromGraphItem chromGraphItem)
        {
            var chromDisplayState = _displayState as ChromDisplayState;
            if (chromDisplayState != null)
            {
                chromDisplayState.ChromGraphItems.Add(
                    new KeyValuePair<PaneKey, ChromGraphItem>(paneKey, chromGraphItem));
                return GraphControl.AddGraphItem(chromDisplayState.GetOrCreateGraphPane(GraphControl, paneKey),
                    chromGraphItem, false);
            }
            else
            {
                return null;
            }
        }

        public MSGraphPane GetGraphPane(PaneKey paneKey)
        {
            return _displayState.GetGraphPane(GraphControl, paneKey);
        }

        public CurveItem AddSpectrum(AbstractSpectrumGraphItem item, bool refresh=true)
        {
            var pane = _displayState.GetOrCreateGraphPane(GraphControl, PaneKey.DEFAULT);
            pane.Title.Text = item.Title;
            var curveItem = GraphControl.AddGraphItem(pane, item);
            curveItem.Label.IsVisible = false;
            pane.Legend.IsVisible = false;
            if (refresh)
                GraphControl.Refresh();
            return curveItem;
        }

        public CurveItem SetErrorGraphItem(IMSGraphItemInfo msGraphItem)
        {
            return SetErrorGraphItems(new[] {msGraphItem}).First();
        }

        public IEnumerable<CurveItem> SetErrorGraphItems(IEnumerable<IMSGraphItemInfo> errorItems)
        {
            var curveItems = new List<CurveItem>();
            SetDisplayState(new ErrorDisplayState());
            var pane = _displayState.GetOrCreateGraphPane(GraphControl, PaneKey.DEFAULT);
            pane.Legend.IsVisible = false;
            foreach (var msGraphItem in errorItems)
            {
                var curveItem = GraphControl.AddGraphItem(pane, msGraphItem);
                curveItem.Label.IsVisible = false;
                curveItems.Add(curveItem);
                pane.Title.Text = msGraphItem.Title;
            }
            GraphControl.AxisChange();
            GraphControl.Invalidate();
            return curveItems;
        }

        public bool AllowSplitGraph
        {
            get { return _displayState.AllowSplitPanes; }
        }

        public void ZoomToPeak(double startRetentionTime, double endRetentionTime)
        {
            var retentionTimeValues = new RetentionTimeValues((startRetentionTime + endRetentionTime) / 2,
                startRetentionTime, endRetentionTime, 0, null);
            ZoomToPeaks(retentionTimeValues, retentionTimeValues);
        }

        private void ZoomToPeaks(RetentionTimeValues firstPeak, RetentionTimeValues lastPeak)
        {
            var chromDisplayState = _displayState as ChromDisplayState;
            if (chromDisplayState == null)
            {
                return;
            }
            var bestStartTime = firstPeak.StartRetentionTime;
            var bestEndTime = lastPeak.EndRetentionTime;
            // If relative zooming, scale to the best peak
            if (chromDisplayState.TimeRange == 0 || chromDisplayState.PeakRelativeTime)
            {
                double multiplier = (chromDisplayState.TimeRange != 0 ? chromDisplayState.TimeRange : GraphChromatogram.DEFAULT_PEAK_RELATIVE_WINDOW);
                bestStartTime -= firstPeak.Fwb * (multiplier - 1) / 2;
                bestEndTime += lastPeak.Fwb * (multiplier - 1) / 2;
            }
            // Otherwise, use an absolute peak width
            else
            {
                double mid = (bestStartTime + bestEndTime) / 2;
                bestStartTime = mid - chromDisplayState.TimeRange / 2;
                bestEndTime = bestStartTime + chromDisplayState.TimeRange;
            }
            ZoomXAxis(bestStartTime, bestEndTime);
        }

        public abstract class DisplayState
        {
            protected DisplayState(IEnumerable<TransitionGroup> transitionGroups)
            {
                TransitionGroups = transitionGroups == null ? new TransitionGroup[0] : transitionGroups.ToArray();
                GraphPaneKeys = new List<PaneKey>();
            }
            protected TransitionGroup[] TransitionGroups { get; private set; }
            public abstract bool CanUseZoomStateFrom(DisplayState displayStatePrev);
            public bool ZoomStateValid { get; set; }
            public List<PaneKey> GraphPaneKeys { get; private set; }
            public bool AllowSplitPanes { get; protected set; }
            public bool ShowLegend { get; protected set; }
            public MSGraphPane GetGraphPane(MSGraphControl graphControl, PaneKey graphPaneKey)
            {
                if (!AllowSplitPanes)
                {
                    return graphControl.GraphPane;
                }
                int index = GraphPaneKeys.BinarySearch(graphPaneKey);
                if (index >= 0)
                {
                    return (MSGraphPane) graphControl.MasterPane.PaneList[index];
                }
                return null;
            }
            public MSGraphPane GetOrCreateGraphPane(MSGraphControl graphControl, PaneKey graphPaneKey)
            {
                var pane = GetGraphPane(graphControl, graphPaneKey);
                if (null != pane)
                {
                    return pane;
                }
                if (GraphPaneKeys.Count == 0)
                {
                    GraphPaneKeys.Add(graphPaneKey);
                    pane = graphControl.GraphPane;
                    ApplySettingsToGraphPane(pane);
                    return pane;
                }
                int index = GraphPaneKeys.BinarySearch(graphPaneKey);
                int iInsert = ~index;
                var graphPane = InsertMsGraphPane(graphControl, iInsert);
                GraphPaneKeys.Insert(iInsert, graphPaneKey);
                using (var graphics = graphControl.CreateGraphics())
                {
                    graphControl.MasterPane.SetLayout(graphics, PaneLayout.SingleColumn);
                }
                return graphPane;
            }
            private MSGraphPane InsertMsGraphPane(MSGraphControl graphControl, int iInsert)
            {
                var pane = new MSGraphPane
                {
                    Border = { IsVisible = false },
                    AllowCurveOverlap = true,
                };
                ApplySettingsToGraphPane(pane);
                var primaryPane = graphControl.GraphPane;
                pane.CurrentItemType = primaryPane.CurrentItemType;
                pane.ZoomStack.AddRange(primaryPane.ZoomStack);
                var zoomState = new ZoomState(primaryPane, ZoomState.StateType.Zoom);
                zoomState.ApplyState(pane);
                pane.YAxis.Title.Text = primaryPane.YAxis.Title.Text;
                pane.XAxis.Title.Text = primaryPane.XAxis.Title.Text;
                graphControl.MasterPane.PaneList.Insert(iInsert, pane);
                return pane;
            }
            /// <summary>
            /// Resets the all of the properties of a graph pane that might have changed 
            /// (or initialize the properties of a newly  created pane) so that it's ready 
            /// to be used by a different set of graphs.
            /// </summary>
            public virtual void ApplySettingsToGraphPane(MSGraphPane graphPane)
            {
                graphPane.Legend.IsVisible = ShowLegend;
                graphPane.Title.IsVisible = true;
                graphPane.Title.Text = null;
                graphPane.AllowLabelOverlap = true; // Always allow labels to overlap - they're transparent so it's OK to do so
            }
        }

        public class ChromDisplayState : DisplayState
        {
            private readonly bool _proteinSelected;

            public ChromDisplayState(Settings settings, TransformChrom transformChrom, IEnumerable<TransitionGroup> transitionGroups, bool proteinSelected, bool forceLegendDisplay = false) : base(transitionGroups)
            {
                AutoZoomChrom = GraphChromatogram.AutoZoom;
                TransformChrom = transformChrom;
                MinIntensity = settings.ChromatogramMinIntensity;
                MaxIntensity = settings.ChromatogramMaxIntensity;
                TimeRange = settings.ChromatogramTimeRange;
                PeakRelativeTime = settings.ChromatogramTimeRangeRelative;
                AllowSplitPanes = settings.SplitChromatogramGraph;
                ChromGraphItems = new List<KeyValuePair<PaneKey, ChromGraphItem>>();
                ShowLegend = forceLegendDisplay || settings.ShowChromatogramLegend;
                _proteinSelected = proteinSelected;
            }
            
            public AutoZoomChrom AutoZoomChrom { get; private set; }
            public TransformChrom TransformChrom { get; }
            public double MinIntensity { get; private set; }
            public double MaxIntensity { get; private set; }
            public double TimeRange { get; private set; }
            public bool PeakRelativeTime { get; private set; }
            public IList<KeyValuePair<PaneKey, ChromGraphItem>> ChromGraphItems { get; private set; }

            public override bool CanUseZoomStateFrom(DisplayState displayStatePrev)
            {
                var prevChromDisplayState = displayStatePrev as ChromDisplayState;
                if (null == prevChromDisplayState)
                {
                    return false;
                }

                if (TransformChrom.IsDerivative() || prevChromDisplayState.TransformChrom.IsDerivative())
                {
                    // The Y-axis range of different derivatives is very different, so we need to recalculate the zoom
                    // state if the transformation has changed and it is or was a derivative
                    if (TransformChrom != prevChromDisplayState.TransformChrom)
                    {
                        return false;
                    }
                }
                if (Equals(AutoZoomChrom, prevChromDisplayState.AutoZoomChrom) &&
                    Equals(MinIntensity, prevChromDisplayState.MinIntensity) &&
                    Equals(MaxIntensity, prevChromDisplayState.MaxIntensity) &&
                    Equals(TimeRange, prevChromDisplayState.TimeRange) &&
                    Equals(PeakRelativeTime, prevChromDisplayState.PeakRelativeTime) &&
                    _proteinSelected == prevChromDisplayState._proteinSelected)
                {
                    return ArrayUtil.ReferencesEqual(TransitionGroups, prevChromDisplayState.TransitionGroups);
                }
                return false;
            }
        }

        public class SpectrumDisplayState : DisplayState
        {
            public SpectrumDisplayState(Settings settings, IEnumerable<TransitionGroup> transitionGroups)
                : base(transitionGroups)
            {
            }

            public override bool CanUseZoomStateFrom(DisplayState displayStatePrev)
            {
                var prevSpectrumDisplayState = displayStatePrev as SpectrumDisplayState;
                if (null != prevSpectrumDisplayState)
                {
                    return true;
                }
                return false;
            }
        }

        public class ErrorDisplayState : DisplayState
        {
            public ErrorDisplayState() : base(null)
            {
            }

            public override bool CanUseZoomStateFrom(DisplayState displayStatePrev)
            {
                return false;
            }
        }

        public static void FormatGraphPane(GraphPane zedGraphPane)
        {
            if (zedGraphPane != null && zedGraphPane.YAxis != null && zedGraphPane.YAxis.Scale != null)
            {
                if (Settings.Default.UsePowerOfTen)
                {
                    zedGraphPane.YAxis.Scale.Format = SCIENTIFIC_NOTATION_FORMAT_STRING;
                    zedGraphPane.YAxis.Scale.MagAuto = false;
                    zedGraphPane.YAxis.Scale.Mag = 0;
                }
                else
                {
                    zedGraphPane.YAxis.Scale.Format = @"g";
                    zedGraphPane.YAxis.Scale.MagAuto = true;
                }
            }
        }

        public static void FormatFontSize(GraphPane g, float fontSize)
        {
            g.YAxis.Scale.FontSpec.Size = fontSize*1.25f;
            g.XAxis.Scale.FontSpec.Size = fontSize*1.25f;
            g.YAxis.Title.FontSpec.Size = fontSize*1.2f;
            g.XAxis.Title.FontSpec.Size = fontSize*1.2f;
            g.Title.FontSpec.Size = fontSize*1.5f;
            g.Legend.FontSpec.Size = fontSize;
        }

        public static void ReformatYAxis(GraphPane g, double myMaxY)
        {
            var _max = MyMod(myMaxY, g.YAxis.Scale.MajorStep) == 0.0 ? myMaxY :
                  myMaxY + g.YAxis.Scale.MajorStep - MyMod(myMaxY, g.YAxis.Scale.MajorStep);
            g.YAxis.Scale.Max = _max;
        }
        protected static double MyMod(double x, double y)
        {
            if (y == 0)
                return 0;
            var temp = x / y;
            return y * (temp - Math.Floor(temp));
        }

        // Find maximum value for bar graph including whiskers
        public static double GetMaxY(CurveList curveList, GraphPane g)
        {
            var maxY = double.MinValue;
            foreach (var curve in curveList.FindAll(curve => !curve.IsY2Axis))
            {
                if (curve is MeanErrorBarItem)
                {
                    for (var i = 0; i < curve.Points.Count; i++)
                    {
                        var point = curve.Points[i];
                        if (point.IsMissing)
                        {
                            continue;
                        }
                        if (!double.IsNaN(point.Y))
                        {
                            var errorTag = point.Tag as ErrorTag;
                            double whiskerMaxY;
                            if (null != errorTag)
                            {
                                whiskerMaxY = point.Y + errorTag.Error;
                            }
                            else
                            {
                                whiskerMaxY = point.Y;
                            }

                            maxY = Math.Max(maxY, whiskerMaxY);
                        }
                    }
                }

                double tMaxY;
                curveList.GetCurveRange(g, curve, out _, out _, out _, out tMaxY);

                maxY = Math.Max(maxY, tMaxY);
            }
            return maxY;
        }
        public static Color Blend(Color baseColor, Color blendColor, double blendAmount)
        {
            return Color.FromArgb(
                (int)(baseColor.R * (1 - blendAmount) + blendColor.R * blendAmount),
                (int)(baseColor.G * (1 - blendAmount) + blendColor.G * blendAmount),
                (int)(baseColor.B * (1 - blendAmount) + blendColor.B * blendAmount));
        }
    }

    public struct PaneKey : IComparable
    {
        public static readonly PaneKey PRECURSORS = new PaneKey(Adduct.EMPTY, null, false);
        public static readonly PaneKey PRODUCTS = new PaneKey(Adduct.EMPTY, null, true);
        public static readonly PaneKey DEFAULT = new PaneKey(Adduct.EMPTY, null, null);

        public PaneKey(TransitionGroupDocNode nodeGroup)
            : this(nodeGroup != null ? nodeGroup.TransitionGroup.PrecursorAdduct : Adduct.EMPTY,
                   nodeGroup != null ? nodeGroup.TransitionGroup.LabelType : null,
                   false)
        {
            SpectrumClassFilter = nodeGroup?.SpectrumClassFilter ?? default;
        }

        public PaneKey(IsotopeLabelType isotopeLabelType)
            : this(Adduct.EMPTY, isotopeLabelType, false)
        {
        }

        private PaneKey(Adduct precursorAdduct, IsotopeLabelType isotopeLabelType, bool? isProducts)
            : this()
        {
            PrecursorAdduct = precursorAdduct.Unlabeled; // Interested only in the "+2Na" part of "M3C13+2Na"
            IsotopeLabelType = isotopeLabelType;
            IsProducts = isProducts;
        }

        public Adduct PrecursorAdduct { get; private set; }
        public IsotopeLabelType IsotopeLabelType { get; private set; }
        public SpectrumClassFilter? SpectrumClassFilter { get; private set; }
        public bool? IsProducts { get; private set; }

        private Tuple<Adduct, IsotopeLabelType, SpectrumClassFilter?, bool?> AsTuple()
        {
            return Tuple.Create(PrecursorAdduct, IsotopeLabelType, SpectrumClassFilter, IsProducts);
        }

        public int CompareTo(object other)
        {
            return Comparer.Default.Compare(AsTuple(), ((PaneKey)other).AsTuple());
        }

        public bool IncludesTransitionGroup(TransitionGroupDocNode transitionGroupDocNode)
        {
            if (!PrecursorAdduct.IsEmpty &&
                !Equals(PrecursorAdduct, transitionGroupDocNode.TransitionGroup.PrecursorAdduct.Unlabeled)) // Compare adducts without any embedded isotope info
            {
                return false;
            }
            if (null != IsotopeLabelType &&
                !Equals(IsotopeLabelType, transitionGroupDocNode.TransitionGroup.LabelType))
            {
                return false;
            }

            if (SpectrumClassFilter.HasValue && !Equals(SpectrumClassFilter, transitionGroupDocNode.SpectrumClassFilter))
            {
                return false;
            }
            return true;
        }
    }    
}
