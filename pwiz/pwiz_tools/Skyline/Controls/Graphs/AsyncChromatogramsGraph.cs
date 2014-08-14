/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
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
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using ZedGraph;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Controls.Graphs
{
    /// <summary>
    /// Asynchronously render the graph for AllChromatogramsGraph.
    /// </summary>
    class AsyncChromatogramsGraph : AsyncRenderControl
    {
        private const int MAX_FRAMES_PER_SECOND = 10;           // target animation speed
        private const int STEPS_FOR_INTENSITY_ANIMATION = 5;    // half a second for growing peaks and adjusting intensity axis
        private const int STEPS_FOR_TIME_AXIS_ANIMATION = 10;   // one second for adjusting time axis
        private const double MINUTES_PER_BIN = 0.5;             // resolution for intensity averaging
        private const int MAX_PEAKS_PER_BIN = 4;                // how many peaks to graph per bin
        private const double DISPLAY_FILTER_PERCENT = 0.005;    // filter peaks less than this percentage of maximum intensity
        
        private const float CURVE_LINE_WIDTH = 1.0f;            // width of line used to graph peaks
        private const float PROGRESS_LINE_WIDTH = 2.0f;         // width of line to show current progress for progressive graphs
        private const double X_AXIS_START = 1.0;                // initial value for time axis
        private const double Y_AXIS_START = 1.0;                // initial value for intensity axis

        private readonly Color _backgroundGradientColor1 = Color.FromArgb(240, 250, 250);
        private readonly Color _backgroundGradientColor2 = Color.FromArgb(250, 250, 210);
        private readonly Color _unfinishedLineColor = Color.FromArgb(180, 180, 180);
        private readonly Color _unknownPeakColor = Color.FromArgb(150, 150, 150);

        // Access only on background thread
        private GraphPane _graphPane;
        private double _xMax;
        private double _yMax;
        private Animation _xAxisAnimation;
        private Animation _yAxisAnimation;
        private BoxObj _unfinishedBox;
        private LineObj _unfinishedLine;
        private readonly List<ChromatogramLoadingStatus.TransitionData.Peak[]> _bins =
            new List<ChromatogramLoadingStatus.TransitionData.Peak[]>(); 
        private readonly List<CurveInfo> _animatingCurves = new List<CurveInfo>();
        private double _lastCurrentTime;
        private ChromatogramLoadingStatus _status;
        private ChromatogramLoadingStatus _newStatus;

        //private static readonly Log LOG = new Log<AsyncChromatogramsGraph>();

        public AsyncChromatogramsGraph()
            : base(MAX_FRAMES_PER_SECOND, "AllChromatograms background render") // Not L10N
        {
        }

        /// <summary>
        /// Initialize graph renderer on the background thread.
        /// </summary>
        protected override void BackgroundInitialize()
        {
            _graphPane = new GraphPane();
            _graphPane.Chart.Fill = new Fill(_backgroundGradientColor1, _backgroundGradientColor2, 45.0f);
            _graphPane.Chart.Border.IsVisible = false;
            _graphPane.Border.IsVisible = false;

            _graphPane.XAxis.Title.Text = Resources.AsyncChromatogramsGraph_AsyncChromatogramsGraph_Retention_Time__min__;
            _graphPane.XAxis.MinorTic.IsOpposite = false;
            _graphPane.XAxis.MajorTic.IsOpposite = false;
            _graphPane.XAxis.Scale.Min = 0.0;
            _graphPane.XAxis.Scale.Max = _xMax = X_AXIS_START;

            _graphPane.YAxis.Title.Text = Resources.AsyncChromatogramsGraph_AsyncChromatogramsGraph_Intensity;
            _graphPane.YAxis.MinorTic.IsOpposite = false;
            _graphPane.YAxis.MajorTic.IsOpposite = false;
            _graphPane.YAxis.Scale.Min = 0.0;
            _graphPane.YAxis.Scale.Max = _yMax = Y_AXIS_START;
        }

        /// <summary>
        /// Clear the graph and track new status object.
        /// </summary>
        /// <param name="status"></param>
        public void ClearGraph(ChromatogramLoadingStatus status)
        {
            lock (this)
            {
                _newStatus = status;
            }
        }

        /// <summary>
        /// Render the graph on the background thread.
        /// </summary>
        /// <param name="width">Width in pixels.</param>
        /// <param name="height">Height in pixels.</param>
        /// <param name="forceRender">True to force rendering (usually after size change).</param>
        /// <param name="bitmap">Returns rendered bitmap.</param>
        protected override void Render(int width, int height, bool forceRender, ref Bitmap bitmap)
        {
            lock (this)
            {
                // If we have a new status object, clear the graph, set the title to the new file name,
                // reset the range of axes, etc.
                if (_newStatus != null)
                {
                    _status = _newStatus;
                    _newStatus = null;
                    string sampleName = _status.FilePath.GetSampleName();
                    string filePath = _status.FilePath.GetFileName();
                    var fileName = !string.IsNullOrEmpty(sampleName)
                            ? string.Format(Resources.AsyncChromatogramsGraph_Render__0___sample__1_, filePath, sampleName)
                            : filePath;
                    _graphPane.Title.Text = fileName;
                    _graphPane.CurveList.Clear();
                    _graphPane.XAxis.Scale.Max = _xMax = X_AXIS_START;
                    _graphPane.YAxis.Scale.Max = _yMax = Y_AXIS_START;
                    _graphPane.AxisChange();
                    _xAxisAnimation = null;
                    _yAxisAnimation = null;
                    _bins.Clear();
                    _animatingCurves.Clear();
                    if (_status.Transitions != null)
                        _status.Transitions.CurrentTime = 0;
                    forceRender = true;
                }
            }

            if (_status == null)
                return;

            if (_status.Transitions != null && bitmap != null)  // don't add data until we've rendered first bitmap
            {
                // We need to process data even if the control isn't visible to reduce
                // the memory load of raw chromatogram data.
                forceRender = AddData(_status.Transitions) || forceRender;

                if (!IsVisible)
                    return;

                // Animate growing curves and changing axis scales.
                forceRender = Animate() || forceRender;

                // For progressive import, update the progress line.
                forceRender = UpdateProgressLine() || forceRender;
            }

            // Render a new bitmap if something has changed.
            if (forceRender)
            {
                var newBitmap = new Bitmap(width, height);
                using (var graphics = Graphics.FromImage(newBitmap))
                {
                    _graphPane.ReSize(graphics, new RectangleF(0, 0, width, height));
                    _graphPane.Draw(graphics);
                }
                bitmap = newBitmap;
            }
        }

        /// <summary>
        /// Add peaks to the graph.
        /// </summary>
        /// <returns>True if render is needed.</returns>
        private bool AddData(ChromatogramLoadingStatus.TransitionData transitions)
        {
            bool render = false;

            // Render progress line if it has moved.
            if (transitions.Progressive && transitions.CurrentTime > _lastCurrentTime)
            {
                _lastCurrentTime = transitions.CurrentTime;
                render = true;
            }

            // Add new curves and points.
            var finishedPeaks = transitions.GetPeaks();
            foreach (var peak in finishedPeaks)
            {
                // Discard tiny peaks.
                if (peak.PeakIntensity < transitions.MaxIntensity*DISPLAY_FILTER_PERCENT)
                    continue;

                if (!peak.MayOverlap)
                {
                    render = AddPeak(peak, 0.0) || render;
                    continue;
                }

                // For SRM data, we need to combine peaks for the same filter index that overlap in time.
                // When we do that, the new combined peak should grow from its current height, not from
                // the base of the graph again.  Here we combine peaks and calculate a scale factor to
                // start animation from so that the peak maintains its current height.
                var removePeaks = new List<ChromatogramLoadingStatus.TransitionData.Peak>();
                foreach (var bin in _bins)
                {
                    if (bin == null)
                        continue;
                    for (int i = 0; i < MAX_PEAKS_PER_BIN; i++)
                    {
                        var displayedPeak = bin[i];
                        if (displayedPeak == null)
                            break;
                        if (displayedPeak.FilterIndex == peak.FilterIndex && peak.Overlaps(displayedPeak))
                        {
                            // Combine old peak curve with the new peak.
                            peak.Add(displayedPeak);

                            // Remove old peak from display list.
                            _graphPane.CurveList.Remove(((CurveInfo) displayedPeak.CurveInfo).Curve);

                            // Remember old peak so we can correctly animate the new one.
                            removePeaks.Add(displayedPeak);

                            // Compact this bin.
                            for (int j = i; j < MAX_PEAKS_PER_BIN - 1; j++)
                                bin[j] = bin[j + 1];
                            bin[MAX_PEAKS_PER_BIN - 1] = null;
                            break;
                        }
                    }
                }

                // Break peak curve into multiple peaks that can be individually animated.
                foreach (var extractedPeak in ExtractPeaks(peak))
                {
                    double animatedScaleFactor = 0.0;
                    var extractedTimes = extractedPeak.Times;
                    double startTime = extractedTimes[0];
                    double endTime = extractedTimes[extractedTimes.Count - 1];
                    foreach (var removedPeak in removePeaks)
                    {
                        var removedTimes = removedPeak.Times;
                        if (removedTimes[0] >= startTime && removedTimes[removedTimes.Count - 1] <= endTime)
                        {
                            animatedScaleFactor = removedPeak.PeakIntensity/extractedPeak.PeakIntensity;
                            if (((CurveInfo)removedPeak.CurveInfo).Animation != null)
                                animatedScaleFactor *= ((CurveInfo)removedPeak.CurveInfo).Animation.Value;
                            break;
                        }
                    }
                    render = AddPeak(extractedPeak, animatedScaleFactor) || render;
                }
            }

            if (IsVisible)
            {
                // Rescale axes to new maximum values.
                double maxRetentionTime = transitions.MaxRetentionTime;
                if (!transitions.MaxRetentionTimeKnown)
                    maxRetentionTime *= 1.1;
                render = AnimateAxes(maxRetentionTime, transitions.MaxIntensity*1.1) || render;
            }

            return render;
        }

        private int GetBinIndex(ChromatogramLoadingStatus.TransitionData.Peak peak)
        {
            return (int)Math.Round(peak.PeakTime/MINUTES_PER_BIN);
        }

        private bool AddPeak(ChromatogramLoadingStatus.TransitionData.Peak peak, double animatedScaleFactor)
        {
            // Find the time bin for peaks with this start time.
            var binIndex = GetBinIndex(peak);
            while (binIndex >= _bins.Count)
                _bins.Add(null);
            var bin = _bins[binIndex];

            // Create a new bin.
            if (bin == null)
            {
                bin = _bins[binIndex] = new ChromatogramLoadingStatus.TransitionData.Peak[MAX_PEAKS_PER_BIN];
                bin[0] = peak;
            }

            // Fill an empty slot in the bin.
            else if (bin[MAX_PEAKS_PER_BIN - 1] == null)
            {
                for (int i = 0;; i++)
                {
                    if (bin[i] == null)
                    {
                        bin[i] = peak;
                        break;
                    }
                }
            }

            // If there is a peak with a lower intensity, replace it with the new peak.
            else
            {
                int min = 0;
                double minIntensity = bin[0].PeakIntensity;
                for (int i = 1; i < MAX_PEAKS_PER_BIN; i++)
                {
                    double peakIntensity = bin[i].PeakIntensity;
                    if (minIntensity > peakIntensity)
                    {
                        minIntensity = peakIntensity;
                        min = i;
                    }
                }
                if (peak.PeakIntensity < minIntensity)
                    return false;   // no render required
                var info = (CurveInfo) bin[min].CurveInfo;
                _graphPane.CurveList.Remove(info.Curve);
                bin[min] = peak;
            }

            if (_status.Transitions.MaxIntensity < peak.PeakIntensity)
                _status.Transitions.MaxIntensity = peak.PeakIntensity;

            // Drop low-intensity points.
            FilterLowIntensityPoints(peak);

            // Determine z-order for this peak (lowest intensity peaks are in front).
            int zIndex = 0;
            for (int i = 0; i < _bins.Count; i++)
            {
                bin = _bins[i];
                if (bin != null)
                {
                    for (int j = 0; j < MAX_PEAKS_PER_BIN; j++)
                    {
                        if (bin[j] == null)
                            break;
                        if (bin[j].PeakIntensity < peak.PeakIntensity)
                            zIndex++;
                    }
                }
            }

            // Add new peak to display list.
            var curveInfo = new CurveInfo { Peak = peak, Animation = new Animation(animatedScaleFactor, 1.0, STEPS_FOR_INTENSITY_ANIMATION, 1000 / MAX_FRAMES_PER_SECOND) };
            peak.CurveInfo = curveInfo;
            _animatingCurves.Add(curveInfo);
            NewCurve(curveInfo, zIndex);

            return true;    // render needed
        }

        private void FilterLowIntensityPoints(ChromatogramLoadingStatus.TransitionData.Peak peak)
        {
            // Count number of points that fall below intensity threshold for this peak.
            double threshold = peak.PeakIntensity*0.01;
            int start = 0;
            while (peak.Intensities[start] < threshold)
                start++;
            int end = peak.Intensities.Count - 1;
            while (peak.Intensities[end] < threshold)
                end--;
            end++;
            int remaining = end - start;
            if (remaining == peak.Intensities.Count)
                return;

            Assume.IsTrue(remaining > 0);

            // Compact list, omitting low intensity points.
            var times = new List<float>(remaining);
            var intensities = new List<float>(remaining);
            for (int i = start; i < end; i++)
            {
                times.Add(peak.Times[i]);
                intensities.Add(peak.Intensities[i]);
            }
            peak.Times = times;
            peak.Intensities = intensities;
        }

        /// <summary>
        /// Add a new curve (representing a peak) to the graph.
        /// </summary>
        private void NewCurve(CurveInfo curveInfo, int zIndex)
        {
            const int lineTransparency = 200;
            const int fillTransparency = 90;

            var peak = curveInfo.Peak;
            int peakId = peak.FilterIndex;
            var color = ColorGenerator.GetColor(peak.ModifiedSequence);
            var curve = curveInfo.Curve = new LineItem(peakId + string.Empty, new PointPairList(), color, SymbolType.None);
            curve.Label.IsVisible = false;
            curve.Line.Color = Color.FromArgb(peakId == 0 ? 70 : lineTransparency, color);
            curve.Line.Width = CURVE_LINE_WIDTH;
            curve.Line.Style = DashStyle.Solid;
            var fillColor = Color.FromArgb(peakId == 0 ? 50 : fillTransparency, color);
            curve.Line.Fill = new Fill(fillColor);
            curve.Line.IsAntiAlias = true;

            // Add leading zero to curve.
            curve.AddPoint(peak.Times[0] - ChromatogramLoadingStatus.TIME_RESOLUTION, 0.0);

            double animationScaleFactor = curveInfo.Animation != null ? curveInfo.Animation.Value : 1.0;
            for (int i = 0; i < peak.Times.Count; i++)
                curve.AddPoint(peak.Times[i], peak.Intensities[i]*animationScaleFactor);

            // Add trailing zero.
            if (!peak.NoTrailingZero)
                curve.AddPoint(peak.Times[peak.Times.Count - 1] + ChromatogramLoadingStatus.TIME_RESOLUTION, 0.0);

            if (zIndex >= _graphPane.CurveList.Count)
                _graphPane.CurveList.Add(curve);
            else
                _graphPane.CurveList.Insert(zIndex, curve);
        }

        /// <summary>
        /// Perform one step of peak and graph axes animations.
        /// </summary>
        /// <returns>True if render is needed.</returns>
        private bool Animate()
        {
            bool render = _animatingCurves.Count > 0 || _xAxisAnimation != null || _yAxisAnimation != null;

            // Animate range of x and y axes.
            if (_xAxisAnimation != null)
            {
                _graphPane.XAxis.Scale.Max = _xAxisAnimation.Step();
                _graphPane.AxisChange();

                if (_xAxisAnimation.Done)
                    _xAxisAnimation = null;
            }

            if (_yAxisAnimation != null)
            {
                _graphPane.YAxis.Scale.Max = _yAxisAnimation.Step();
                _graphPane.AxisChange();

                if (_yAxisAnimation.Done)
                {
                    _yAxisAnimation = null;
                }
            }

            // Animate scale of new transition curves.
            for (int i = _animatingCurves.Count - 1; i >= 0; i--)
            {
                var curveInfo = _animatingCurves[i];
                var animation = curveInfo.Animation;
                animation.Step();
                
                for (int j = 0; j < curveInfo.Peak.Intensities.Count; j++)
                    curveInfo.Curve[j + 1].Y = curveInfo.Peak.Intensities[j] * animation.Value;

                if (animation.Done)
                {
                    curveInfo.Animation = null;
                    _animatingCurves.RemoveAt(i);
                }
            }

            return render;
        }


        private IEnumerable<ChromatogramLoadingStatus.TransitionData.Peak> ExtractPeaks(ChromatogramLoadingStatus.TransitionData.Peak peak)
        {
            var intensities = peak.Intensities;
            int startIndex = 0;
            double thresholdIntensity = _status.Transitions.MaxIntensity * ChromatogramLoadingStatus.INTENSITY_THRESHOLD_PERCENT;

            while (true)
            {
                while (startIndex < intensities.Count && intensities[startIndex] < thresholdIntensity)
                    startIndex++;
                if (startIndex == intensities.Count)
                    yield break;

                // Find end of transition below the threshold intensity value.
                int endIndex = startIndex + 1;
                float peakTime = peak.Times[startIndex];
                float peakIntensity = intensities[startIndex];
                while (endIndex < intensities.Count && intensities[endIndex] >= thresholdIntensity)
                {
                    if (peakIntensity < intensities[endIndex])
                    {
                        peakIntensity = intensities[endIndex];
                        peakTime = peak.Times[endIndex];
                    }
                    endIndex++;
                }

                yield return new ChromatogramLoadingStatus.TransitionData.Peak(peak.ModifiedSequence, peak.FilterIndex, false)
                    {
                        Times = peak.Times.GetRange(startIndex, endIndex - startIndex),
                        Intensities = peak.Intensities.GetRange(startIndex, endIndex - startIndex),
                        PeakTime = peakTime,
                        PeakIntensity = peakIntensity
                    };

                startIndex = endIndex;
            }
        }

        /// <summary>
        /// Determine if graph axes need to be adjusted (animated).
        /// </summary>
        /// <param name="xTarget">Target value for x axis.</param>
        /// <param name="yTarget">Target value for y axis.</param>
        /// <returns>True if render is needed.</returns>
        private bool AnimateAxes(double xTarget, double yTarget)
        {
            bool render = false;

            // Animate the x axis if the range has changed.
            if (_xMax < xTarget)
            {
                _xMax = xTarget;

                // Don't animate on the first range change (too much activity to watch).
                if (_graphPane.XAxis.Scale.Max == X_AXIS_START)
                {
                    _graphPane.XAxis.Scale.Max = _xMax;
                    _graphPane.AxisChange();
                }
                else
                {
                    _xAxisAnimation = new Animation(_graphPane.XAxis.Scale.Max, _xMax, STEPS_FOR_TIME_AXIS_ANIMATION, 1000 / MAX_FRAMES_PER_SECOND);
                }

                render = true;
            }

            // Animate the y axis if the range has changed.
            if (_yMax < yTarget)
            {
                _yMax = yTarget;

                // Don't animate on the first range change (too much activity to watch).
                if (_graphPane.YAxis.Scale.Max == Y_AXIS_START)
                {
                    _graphPane.YAxis.Scale.Max = _yMax;
                    _graphPane.AxisChange();
                }
                else
                {
                    _yAxisAnimation = new Animation(_graphPane.YAxis.Scale.Max, _yMax, STEPS_FOR_INTENSITY_ANIMATION, 1000 / MAX_FRAMES_PER_SECOND);
                }

                render = true;
            }

            return render;
        }

        /// <summary>
        /// Update vertical line the marks current import time for progressively loaded files.
        /// </summary>
        /// <returns>True if render is needed.</returns>
        private bool UpdateProgressLine()
        {
            var render = false;

            // Remove old progressive loading indicators.
            if (_unfinishedBox != null)
            {
                _graphPane.GraphObjList.Remove(_unfinishedBox);
                _graphPane.GraphObjList.Remove(_unfinishedLine);
                _unfinishedBox = null;
                render = true;
            }

            // If we're still loading, create a white rectangle which blocks the fill background, indicating data yet to be loaded.
            double currentTime = _status.Transitions.CurrentTime;
            if (_status.Transitions.Progressive && currentTime < _status.Transitions.MaxRetentionTime)
            {
                _unfinishedBox = new BoxObj(
                    currentTime,
                    _graphPane.YAxis.Scale.Max,
                    _graphPane.XAxis.Scale.Max - currentTime,
                    _graphPane.YAxis.Scale.Max - _graphPane.YAxis.Scale.Min,
                    Color.White, Color.White)
                {
                    Location = { CoordinateFrame = CoordType.AxisXYScale },
                    ZOrder = ZOrder.F_BehindGrid
                };
                _graphPane.GraphObjList.Add(_unfinishedBox);

                // Place a vertical line after the last loaded data.
                _unfinishedLine = new LineObj(
                    _unfinishedLineColor,
                    currentTime,
                    _graphPane.YAxis.Scale.Max,
                    currentTime,
                    _graphPane.YAxis.Scale.Min)
                {
                    Location = { CoordinateFrame = CoordType.AxisXYScale },
                    Line = { Width = PROGRESS_LINE_WIDTH },
                    ZOrder = ZOrder.D_BehindAxis
                };
                _graphPane.GraphObjList.Add(_unfinishedLine);
                render = true;
            }

            return render;
        }

        /// <summary>
        /// Associate one of our transition peaks (Peak) with ZedGraph's curve object (LineItem).
        /// </summary>
        private class CurveInfo
        {
            public LineItem Curve;
            public ChromatogramLoadingStatus.TransitionData.Peak Peak;
            public Animation Animation;
        }
    }
}
