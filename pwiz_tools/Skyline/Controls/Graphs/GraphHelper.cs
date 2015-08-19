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
using System.Linq;
using pwiz.Skyline.Util;
using ZedGraph;
using pwiz.MSGraph;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Controls.Graphs
{
    public class GraphHelper
    {
        private DisplayState _displayState;
        private bool _zoomLocked;
        public const string scientificNotationFormatString = "0.0#####e0"; // Not L10N

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

        public void ResetForChromatograms(IEnumerable<TransitionGroup> transitionGroups, bool proteinSelected = false)
        {
            SetDisplayState(new ChromDisplayState(Settings.Default, transitionGroups, proteinSelected));
        }

        public void FinishedAddingChromatograms(double bestStartTime, double bestEndTime, bool forceZoom,
            double leftPeakWidth = 0, double rightPeakWidth = 0)
        {
            if (!_zoomLocked)
            {
                if (forceZoom || !_displayState.ZoomStateValid)
                {
                    var chromDisplayState = _displayState as ChromDisplayState;
                    if (chromDisplayState != null)
                    {
                        AutoZoomChromatograms(bestStartTime, bestEndTime, leftPeakWidth, rightPeakWidth);
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

        private void AutoZoomChromatograms(double bestStartTime, double bestEndTime, double leftPeakWidth, double rightPeakWidth)
        {
            var chromDisplayState = (ChromDisplayState) _displayState;
            if (chromDisplayState.ZoomStateValid)
            {
                return;
            }
            switch (chromDisplayState.AutoZoomChrom)
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
                    if (bestEndTime != 0)
                    {
                        // If relative zooming, scale to the best peak
                        if (chromDisplayState.TimeRange == 0 || chromDisplayState.PeakRelativeTime)
                        {
                            double multiplier = (chromDisplayState.TimeRange != 0 ? chromDisplayState.TimeRange : GraphChromatogram.DEFAULT_PEAK_RELATIVE_WINDOW);
                            if (leftPeakWidth <= 0)
                                leftPeakWidth = rightPeakWidth = bestEndTime - bestStartTime;
                            bestStartTime -= leftPeakWidth * (multiplier - 1) / 2;
                            bestEndTime += rightPeakWidth * (multiplier - 1) / 2;
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
                    break;
                case AutoZoomChrom.window:
                    {
                        var chromGraph = GetRetentionTimeGraphItem(chromDisplayState);
                        if (chromGraph != null)
                        {
                            // Put predicted RT in center with window occupying 2/3 of the graph
                            double windowHalf = chromGraph.RetentionWindow * 2 / 3;
                            double predictedRT = chromGraph.RetentionPrediction.HasValue
                                                     ? // ReSharper
                                                     chromGraph.RetentionPrediction.Value
                                                     : 0;
                            ZoomXAxis(predictedRT - windowHalf, predictedRT + windowHalf);
                        }
                    }
                    break;
                case AutoZoomChrom.both:
                    {
                        double start = double.MaxValue;
                        double end = 0;
                        if (bestEndTime != 0)
                        {
                            start = bestStartTime;
                            end = bestEndTime;
                        }
                        var chromGraph = GetRetentionTimeGraphItem(chromDisplayState);
                        if (chromGraph != null)
                        {
                            // Put predicted RT in center with window occupying 2/3 of the graph
                            double windowHalf = chromGraph.RetentionWindow * 2 / 3;
                            double predictedRT = chromGraph.RetentionPrediction.HasValue
                                                     ? // ReSharper
                                                     chromGraph.RetentionPrediction.Value
                                                     : 0;
                            // Make sure the peak has enough room to display, since it may be
                            // much narrower than the retention time window.
                            if (end != 0)
                            {
                                start -= windowHalf / 8;
                                end += windowHalf / 8;
                            }
                            start = Math.Min(start, predictedRT - windowHalf);
                            end = Math.Max(end, predictedRT + windowHalf);
                        }
                        if (end > 0)
                            ZoomXAxis(start, end);
                    }
                    break;
            }
            foreach (var graphPane in GraphPanes)
            {
                if (chromDisplayState.MinIntensity == 0)
                {
                    graphPane.LockYAxisAtZero = true;
                    graphPane.YAxis.Scale.MinAuto = true;
                }
                else
                {
                    graphPane.LockYAxisAtZero = false;
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
            var chromDisplayState = (ChromDisplayState) _displayState;
            chromDisplayState.ChromGraphItems.Add(new KeyValuePair<PaneKey, ChromGraphItem>(paneKey, chromGraphItem));
            return GraphControl.AddGraphItem(chromDisplayState.GetOrCreateGraphPane(GraphControl, paneKey), chromGraphItem, false);
        }

        public MSGraphPane GetGraphPane(PaneKey paneKey)
        {
            return _displayState.GetGraphPane(GraphControl, paneKey);
        }

        public CurveItem AddSpectrum(AbstractSpectrumGraphItem item)
        {
            var pane = _displayState.GetOrCreateGraphPane(GraphControl, PaneKey.DEFAULT);
            pane.Title.Text = item.Title;
            var curveItem = GraphControl.AddGraphItem(pane, item);
            curveItem.Label.IsVisible = false;
            pane.Legend.IsVisible = false;
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
            public bool AllowLabelOverlap { get; protected set; }
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
                graphPane.AllowLabelOverlap = AllowLabelOverlap;
            }
        }

        public class ChromDisplayState : DisplayState
        {
            private readonly bool _proteinSelected;

            public ChromDisplayState(Settings settings, IEnumerable<TransitionGroup> transitionGroups, bool proteinSelected) : base(transitionGroups)
            {
                AutoZoomChrom = GraphChromatogram.AutoZoom;
                MinIntensity = settings.ChromatogramMinIntensity;
                MaxIntensity = settings.ChromatogramMaxIntensity;
                TimeRange = settings.ChromatogramTimeRange;
                PeakRelativeTime = settings.ChromatogramTimeRangeRelative;
                AllowSplitPanes = settings.SplitChromatogramGraph;
                ChromGraphItems = new List<KeyValuePair<PaneKey, ChromGraphItem>>();
                ShowLegend = settings.ShowChromatogramLegend;
                AllowLabelOverlap = settings.AllowLabelOverlap;
                _proteinSelected = proteinSelected;
            }
            
            public AutoZoomChrom AutoZoomChrom { get; private set; }
            public double MinIntensity { get; private set; }
            public double MaxIntensity { get; private set; }
            public double TimeRange { get; private set; }
            public bool PeakRelativeTime { get; private set; }
            public IList<KeyValuePair<PaneKey, ChromGraphItem>> ChromGraphItems { get; private set; }

            public override bool CanUseZoomStateFrom(DisplayState displayStatePrev)
            {
                var prevChromDisplayState = displayStatePrev as ChromDisplayState;
                if (null != prevChromDisplayState)
                {
                    if (Equals(AutoZoomChrom, prevChromDisplayState.AutoZoomChrom) &&
                        Equals(MinIntensity, prevChromDisplayState.MinIntensity) &&
                        Equals(MaxIntensity, prevChromDisplayState.MaxIntensity) &&
                        Equals(TimeRange, prevChromDisplayState.TimeRange) &&
                        Equals(PeakRelativeTime, prevChromDisplayState.PeakRelativeTime) &&
                        _proteinSelected == prevChromDisplayState._proteinSelected)
                    {
                        return ArrayUtil.ReferencesEqual(TransitionGroups, prevChromDisplayState.TransitionGroups);
                    }
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
                    zedGraphPane.YAxis.Scale.Format = scientificNotationFormatString;
                    zedGraphPane.YAxis.Scale.MagAuto = false;
                    zedGraphPane.YAxis.Scale.Mag = 0;
                }
                else
                {
                    zedGraphPane.YAxis.Scale.Format = "g"; // Not L10N
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
            foreach (var curve in curveList)
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

                double tMinX, tMinY, tMaxX, tMaxY;
                curveList.GetCurveRange(g, curve, out tMinX, out tMaxX, out tMinY, out tMaxY);

                maxY = Math.Max(maxY, tMaxY);
            }
            return maxY;
        }
    }

    public struct PaneKey : IComparable
    {
        public static readonly PaneKey PRECURSORS = new PaneKey(null, null, false);
        public static readonly PaneKey PRODUCTS = new PaneKey(null, null, true);
        public static readonly PaneKey DEFAULT = new PaneKey(null, null, null);

        public PaneKey(TransitionGroupDocNode nodeGroup)
            : this(nodeGroup != null ? nodeGroup.TransitionGroup.PrecursorCharge : (int?)null,
                   nodeGroup != null ? nodeGroup.TransitionGroup.LabelType : null,
                   false)
        {
        }

        public PaneKey(IsotopeLabelType isotopeLabelType)
            : this(null, isotopeLabelType, false)
        {
        }

        private PaneKey(int? precusorCharge, IsotopeLabelType isotopeLabelType, bool? isProducts)
            : this()
        {
            PrecursorCharge = precusorCharge;
            IsotopeLabelType = isotopeLabelType;
            IsProducts = isProducts;
        }

        public int? PrecursorCharge { get; private set; }
        public IsotopeLabelType IsotopeLabelType { get; private set; }
        public bool? IsProducts { get; private set; }

        private Tuple<int?, IsotopeLabelType, bool?> AsTuple()
        {
            return new Tuple<int?, IsotopeLabelType, bool?>(PrecursorCharge, IsotopeLabelType, IsProducts);
        }

        public int CompareTo(object other)
        {
            return Comparer.Default.Compare(AsTuple(), ((PaneKey)other).AsTuple());
        }

        public bool IncludesTransitionGroup(TransitionGroupDocNode transitionGroupDocNode)
        {
            if (PrecursorCharge.HasValue &&
                PrecursorCharge != transitionGroupDocNode.TransitionGroup.PrecursorCharge)
            {
                return false;
            }
            if (null != IsotopeLabelType &&
                !Equals(IsotopeLabelType, transitionGroupDocNode.TransitionGroup.LabelType))
            {
                return false;
            }
            return true;
        }
    }    
}
