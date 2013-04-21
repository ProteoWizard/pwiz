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

namespace pwiz.Skyline.Controls.Graphs
{
    /// <summary>
    /// Asynchronously render the graph for AllChromatogramsGraph.
    /// </summary>
    class AsyncChromatogramsGraph : AsyncRenderControl
    {
        // This is the interval in milliseconds between frame updates.
        private const int ANIMATION_INTERVAL_MSEC = 200;
        private const int MAX_PEAKS = 1000;
        
        private const float CURVE_LINE_WIDTH = 1.0f;
        private const float PROGRESS_LINE_WIDTH = 2.0f;
        private const double X_AXIS_START = 1.0;
        private const double Y_AXIS_START = 1.0;

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
        private double _maxLoadedTime;
        private SortedSet<CurveInfo> _displayedPeaks = new SortedSet<CurveInfo>();
        private readonly List<CurveInfo> _animatingCurves = new List<CurveInfo>();
        private double _maxPeakIntensity;
        private double _lastCurrentTime;
        private ChromatogramLoadingStatus _status;
        private ChromatogramLoadingStatus _newStatus;

        public AsyncChromatogramsGraph()
            : base(ANIMATION_INTERVAL_MSEC)
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
                    string sampleName = SampleHelp.GetPathSampleNamePart(_status.FilePath);
                    string filePath = SampleHelp.GetFileName(_status.FilePath);
                    var fileName = !string.IsNullOrEmpty(sampleName)
                            ? string.Format(Resources.AsyncChromatogramsGraph_Render__0___sample__1_, filePath, sampleName)
                            : filePath;
                    _graphPane.Title.Text = fileName;
                    _graphPane.CurveList.Clear();
                    _graphPane.XAxis.Scale.Max = _xMax = X_AXIS_START;
                    _graphPane.YAxis.Scale.Max = _yMax = Y_AXIS_START;
                    _xAxisAnimation = null;
                    _yAxisAnimation = null;
                    _displayedPeaks.Clear();
                    _animatingCurves.Clear();
                    if (_status.Transitions != null)
                        _status.Transitions.CurrentTime = 0;
                    _maxPeakIntensity = 0;
                    forceRender = true;
                }
            }

            if (_status == null)
                return;

            if (_status.Transitions != null)
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
            var render = false;

            if (transitions.Progressive && transitions.CurrentTime > _lastCurrentTime)
            {
                _lastCurrentTime = transitions.CurrentTime;
                render = true;
            }

            // Add new curves and points.
            var finishedPeaks = transitions.GetPeaks();
            foreach (var peak in finishedPeaks)
            {
                var animatedScaleFactor = 0.0;
                if (peak.MayOverlap)
                {
                    // For SRM data, we need to combine peaks for the same filter index that overlap in time.
                    // When we do that, the new combined peak should grow from its current height, not from
                    // the base of the graph again.  Here we combine peaks and calculate a scale factor to
                    // start animation from so that the peak maintains its current height.
                    var removePeaks = new List<CurveInfo>();
                    foreach (var displayedPeak in _displayedPeaks)
                    {
                        if (displayedPeak.Peak.FilterIndex == peak.FilterIndex && peak.Overlaps(displayedPeak.Peak))
                        {
                            animatedScaleFactor = displayedPeak.Peak.MaxIntensity;
                            if (displayedPeak.Animation != null)
                                animatedScaleFactor *= displayedPeak.Animation.Value;
                            peak.Add(displayedPeak.Peak);
                            animatedScaleFactor /= peak.MaxIntensity;
                            removePeaks.Add(displayedPeak);
                            render = true;
                        }
                    }

                    // Remove an old peak that has been combined with an overlapping peak.
                    foreach (var removePeak in removePeaks)
                        RemoveCurve(removePeak);
                }

                // Remove the lowest-intensity peak if we've hit the peak limit.
                if (_displayedPeaks.Count == MAX_PEAKS)
                {
                    var lowestCurve = _displayedPeaks.Min;
                    if (peak.MaxIntensity <= lowestCurve.Peak.MaxIntensity)
                        continue;
                    RemoveCurve(lowestCurve);
                }

                // Add new peak.
                var maxTime = peak.Times[peak.Times.Count - 1];
                if (_maxLoadedTime < maxTime)
                    _maxLoadedTime = maxTime;
                if (_maxPeakIntensity < peak.MaxIntensity)
                    _maxPeakIntensity = peak.MaxIntensity;
                var curveInfo = new CurveInfo { Peak = peak, Animation = new Animation(animatedScaleFactor, 1.0) };
                _displayedPeaks.Add(curveInfo);

                // Order insertions into graph list so lower-intensity peaks are displayed in front of higher-intensity peaks.
                var index = 0;
                foreach (var displayedPeak in _displayedPeaks)
                {
                    if (ReferenceEquals(curveInfo, displayedPeak))
                        break;
                    index++;
                }
                _animatingCurves.Add(curveInfo);
                NewCurve(curveInfo, index);
                render = true;
            }

            if (IsVisible)
            {
                // Rescale axes to new maximum values.
                var timeScale = (transitions.Progressive) ? transitions.MaxTime : _maxLoadedTime*1.1;
                render = AnimateAxes(timeScale, _maxPeakIntensity*1.1) || render;
            }

            return render;
        }

        /// <summary>
        /// Add a new curve (representing a peak) to the graph.
        /// </summary>
        private void NewCurve(CurveInfo curveInfo, int index)
        {
            const int lineTransparency = 200;
            const int fillTransparency = 90;

            var peak = curveInfo.Peak;
            var peakId = peak.FilterIndex;
            var color = GetPeakColor(peakId);
            var curve = curveInfo.Curve = new LineItem(peakId + "", new PointPairList(), color, SymbolType.None);
            curve.Label.IsVisible = false;
            curve.Line.Color = Color.FromArgb(peakId == 0 ? 70 : lineTransparency, color);
            curve.Line.Width = CURVE_LINE_WIDTH;
            curve.Line.Style = DashStyle.Solid;
            var fillColor = Color.FromArgb(peakId == 0 ? 50 : fillTransparency, color);
            curve.Line.Fill = new Fill(fillColor);
            curve.Line.IsAntiAlias = true;

            // Add leading zero to curve.
            curve.AddPoint(peak.Times[0] - ChromatogramLoadingStatus.TIME_RESOLUTION, 0.0);

            var animationScaleFactor = curveInfo.Animation != null ? curveInfo.Animation.Value : 1.0;
            for (int i = 0; i < peak.Times.Count; i++)
                curve.AddPoint(peak.Times[i], peak.Intensities[i]*animationScaleFactor);

            // Add trailing zero.
            curve.AddPoint(peak.Times[peak.Times.Count - 1] + ChromatogramLoadingStatus.TIME_RESOLUTION, 0.0);

            _graphPane.CurveList.Insert(index, curve);
        }

        /// <summary>
        /// Remove a peak from the displayed peaks list and the graph.
        /// </summary>
        private void RemoveCurve(CurveInfo curve)
        {
            _displayedPeaks.Remove(curve);
            _graphPane.CurveList.Remove(curve.Curve);
        }

        /// <summary>
        /// Perform one step of peak and graph axes animations.
        /// </summary>
        /// <returns>True if render is needed.</returns>
        private bool Animate()
        {
            var render = _animatingCurves.Count > 0 || _xAxisAnimation != null || _yAxisAnimation != null;

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

                    // Remove low-intensity peaks under the threshold intensity after y axis is done animating.
                    FilterLowIntensityPeaks();
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

        private void FilterLowIntensityPeaks()
        {
            // Discard low-intensity points, possibly breaking one peak into several.
            var oldPeaks = _displayedPeaks;
            _displayedPeaks = new SortedSet<CurveInfo>();
            foreach (var oldPeak in oldPeaks)
                ExtractPeaks(oldPeak);

            // Discard low-intensity peaks and peaks over the count limit.
            var thresholdIntensity = _maxPeakIntensity * ChromatogramLoadingStatus.INTENSITY_THRESHOLD_PERCENT;
            while (_displayedPeaks.Count > 0)
            {
                var minPeak = _displayedPeaks.Min;
                if (_displayedPeaks.Count <= MAX_PEAKS && 
                    minPeak.Peak.MaxIntensity >= thresholdIntensity)
                    break;
                _displayedPeaks.Remove(minPeak);
            }

            // Rebuild curve list and animating list.
            _animatingCurves.Clear();
            _graphPane.CurveList.Clear();
            var index = 0;
            foreach (var displayedPeak in _displayedPeaks)
            {
                if (displayedPeak.Animation != null)
                    _animatingCurves.Add(displayedPeak);

                NewCurve(displayedPeak, index++);
            }
        }

        private void ExtractPeaks(CurveInfo curveInfo)
        {
            var intensities = curveInfo.Peak.Intensities;
            var startIndex = 0;
            var thresholdIntensity = _maxPeakIntensity * ChromatogramLoadingStatus.INTENSITY_THRESHOLD_PERCENT;

            while (true)
            {
                while (startIndex < intensities.Count && intensities[startIndex] < thresholdIntensity)
                    startIndex++;
                if (startIndex == intensities.Count)
                    break;

                // Find end of transition below the threshold intensity value.
                int endIndex = startIndex + 1;
                var maxIntensity = intensities[startIndex];
                while (endIndex < intensities.Count && intensities[endIndex] >= thresholdIntensity)
                    maxIntensity = Math.Max(maxIntensity, intensities[endIndex++]);

                var extractedPeak = new ChromatogramLoadingStatus.TransitionData.Peak(curveInfo.Peak.FilterIndex, false)
                    {
                        Times = curveInfo.Peak.Times.GetRange(startIndex, endIndex - startIndex),
                        Intensities = curveInfo.Peak.Intensities.GetRange(startIndex, endIndex - startIndex),
                        MaxIntensity = maxIntensity
                    };

                var newCurveInfo = new CurveInfo {Animation = curveInfo.Animation, Peak = extractedPeak};
                _displayedPeaks.Add(newCurveInfo);

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
            var render = false;

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
                    _xAxisAnimation = new Animation(_graphPane.XAxis.Scale.Max, _xMax);
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
                    FilterLowIntensityPeaks();
                }
                else
                {
                    _yAxisAnimation = new Animation(_graphPane.YAxis.Scale.Max, _yMax);
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
            var currentTime = _status.Transitions.CurrentTime;
            if (_status.Transitions.Progressive && currentTime < _status.Transitions.MaxTime)
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

        // Generate a pleasant color for a peak by subdividing the hue circle.
        private Color GetPeakColor(int peakId)
        {
            if (peakId == 0)
                return _unknownPeakColor;

            var hue = (peakId*109)%360;

            // Random saturation (but not too bland) and value (but not too dark).
            var saturation = ((peakId*433)%50000)/100000.0 + 0.5;   // Range [0.5..1.0]
            var value = ((peakId*809)%50000)/100000.0 + 0.5;        // Range [0.5..1.0]

            return ColorFromHSV(hue, saturation, value);
        }

        // Return a color for the given hue, saturation, and value.
        private static Color ColorFromHSV(double hue, double saturation, double value)
        {
            int hi = Convert.ToInt32(Math.Floor(hue / 60)) % 6;
            double f = hue / 60 - Math.Floor(hue / 60);

            value = value * 255;
            int v = Convert.ToInt32(value);
            int p = Convert.ToInt32(value * (1 - saturation));
            int q = Convert.ToInt32(value * (1 - f * saturation));
            int t = Convert.ToInt32(value * (1 - (1 - f) * saturation));

            if (hi == 0)
                return Color.FromArgb(255, v, t, p);
            else if (hi == 1)
                return Color.FromArgb(255, q, v, p);
            else if (hi == 2)
                return Color.FromArgb(255, p, v, t);
            else if (hi == 3)
                return Color.FromArgb(255, p, q, v);
            else if (hi == 4)
                return Color.FromArgb(255, t, p, v);
            else
                return Color.FromArgb(255, v, p, q);
        }

        /// <summary>
        /// Associate one of our transition peaks (Peak) with ZedGraph's curve object (LineItem).
        /// </summary>
        private class CurveInfo : IComparable
        {
            public LineItem Curve;
            public ChromatogramLoadingStatus.TransitionData.Peak Peak;
            public Animation Animation;

            public int CompareTo(object obj)
            {
                var other = (CurveInfo) obj;
                var compare = Peak.MaxIntensity.CompareTo(other.Peak.MaxIntensity);
                if (compare == 0)
                    compare = Peak.FilterIndex.CompareTo(other.Peak.FilterIndex);
                return compare;
            }
        }
    }
}
