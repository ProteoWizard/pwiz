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
using System.Threading;
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
        private const int NORMAL_FRAME_MILLISECONDS = 500;
        private const int FAST_FRAME_MILLISECONDS = 100;
        private const int STEPS_FOR_INTENSITY_ANIMATION = 2;    // half a second for growing peaks and adjusting intensity axis
        private const int STEPS_FOR_TIME_AXIS_ANIMATION = 12;   // one second for adjusting time axis
        
        private const float PROGRESS_LINE_WIDTH = 2.0f;         // width of line to show current progress for progressive graphs
        private const double X_AXIS_START = 1.0;                // initial value for time axis
        private const double Y_AXIS_START = 100.0;              // initial value for intensity axis

        private readonly Color _backgroundGradientColor1 = Color.FromArgb(240, 250, 250);
        private readonly Color _backgroundGradientColor2 = Color.FromArgb(250, 250, 210);
        private readonly Color _unfinishedLineColor = Color.FromArgb(180, 180, 180);

        // Access only on background thread
        private GraphPane _graphPane;
        private double _xMax;
        private double _yMax;
        private Animation _xAxisAnimation;
        private Animation _yAxisAnimation;
        private BoxObj _unfinishedBox;
        private LineObj _unfinishedLine;
        private readonly List<CurveInfo> _activeCurves = new List<CurveInfo>();
        private ChromatogramLoadingStatus _status;
        private ChromatogramLoadingStatus _newStatus;
        private double _lastTime;
        private CurveInfo _lastCurve;
        private bool _fullFrame;

        //private static readonly Log LOG = new Log<AsyncChromatogramsGraph>();

        public AsyncChromatogramsGraph()
            : base("AllChromatograms background render") // Not L10N
        {
        }

        /// <summary>
        /// Initialize graph renderer on the background thread.
        /// </summary>
        protected override void BackgroundInitialize()
        {
            _graphPane = new GraphPane();
            _graphPane.Chart.Border.IsVisible = false;
            _graphPane.Border.IsVisible = false;

            _graphPane.XAxis.Title.Text = Resources.AsyncChromatogramsGraph_AsyncChromatogramsGraph_Retention_Time;
            _graphPane.XAxis.MinorTic.IsOpposite = false;
            _graphPane.XAxis.MajorTic.IsOpposite = false;
            _graphPane.XAxis.Scale.Min = 0.0;
            _graphPane.XAxis.Scale.Max = _xMax = X_AXIS_START;

            _graphPane.YAxis.Title.Text = Resources.AsyncChromatogramsGraph_AsyncChromatogramsGraph_Intensity;
            _graphPane.YAxis.MinorTic.IsOpposite = false;
            _graphPane.YAxis.MajorTic.IsOpposite = false;
            _graphPane.YAxis.Scale.Min = 0.0;
            _graphPane.YAxis.Scale.Max = _yMax = Y_AXIS_START;
            GraphHelper.FormatGraphPane(_graphPane);

            _lastTime = 0;
        }

        /// <summary>
        /// Clear the graph and track new status object.
        /// </summary>
        /// <param name="status"></param>
        public void ClearGraph(ChromatogramLoadingStatus status)
        {
            _newStatus = status;
        }

        /// <summary>
        /// Render the graph on the background thread.
        /// </summary>
        /// <param name="bitmap">Destination bitmap.</param>
        /// <param name="fullFrame">True to force full frame rendering.</param>
        protected override Rectangle Render(Bitmap bitmap, bool fullFrame)
        {
            _fullFrame = fullFrame;

            var newStatus = Interlocked.Exchange(ref _newStatus, null);
            if (newStatus != null)
            {
                _status = newStatus;
                ResetGraph();
            }

            if (_status == null)
                return Rectangle.Empty;

            // We need to process data even if the control isn't visible to reduce
            // the memory load of raw chromatogram data.
            bool newPeaks = false;
            double time = _lastTime;
            bool progressive = false;
            if (_status.Transitions != null)
            {
                progressive = _status.Transitions.Progressive;
                time = _status.Transitions.CurrentTime;
                newPeaks = AddData(_status.Transitions);
            }

            if (!IsVisible)
                return Rectangle.Empty;

            Rectangle invalidRect;
            if (_fullFrame || _xAxisAnimation != null || _yAxisAnimation != null || (newPeaks && !progressive) || time < _lastTime)
            {
                // full frame invalidation
                invalidRect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            }
            else if (newPeaks || time > _lastTime)
            {
                var p1 = _graphPane.GeneralTransform(_lastTime, 0, CoordType.AxisXYScale);
                var p2 = _graphPane.GeneralTransform(time, 0, CoordType.AxisXYScale);
                int x = (int) p1.X - 1;
                int y = 0;
                int width = (int)(p2.X + PROGRESS_LINE_WIDTH) - x;
                int height = (int) p1.Y + 2;
                invalidRect = new Rectangle(x, y, width, height);
            }
            else
            {
                invalidRect = Rectangle.Empty;
            }

            // Animate changing axis scales.
            Animate();

            // For progressive import, update the progress line.
            if (progressive)
            {
                _lastTime = time;
                UpdateProgressLine(time);
                if (_unfinishedBox == null)
                    invalidRect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            }

            // Render a new bitmap if something has changed.
            if (invalidRect.Width > 0 && _status.Transitions != null &&
                (_status.Transitions.CurrentTime > 0 || _status.Transitions.MaxRetentionTime > 0))
            {
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.SetClip(invalidRect);
                    _graphPane.ReSize(graphics, new RectangleF(0, 0, bitmap.Width, bitmap.Height));
                    _graphPane.Draw(graphics);
                }
            }

            return invalidRect;
        }

        /// <summary>
        /// Add peaks to the graph.
        /// </summary>
        /// <returns>True if render is needed.</returns>
        private bool AddData(ChromatogramLoadingStatus.TransitionData transitions)
        {
            List<ChromatogramLoadingStatus.TransitionData.Peak> bin;
            bool peaksAdded = false;

            // Process bins of peaks queued by the reader thread.
            float maxTime = (float) Math.Max(Math.Max(_xMax, transitions.MaxRetentionTime), transitions.CurrentTime);
            float maxIntensity = 0;
            while (transitions.BinnedPeaks.TryPeek(out bin))
            {
                transitions.BinnedPeaks.TryDequeue(out bin);
                
                if (bin == null)
                {
                    ResetGraph();
                    maxTime = (float) _xMax;
                    maxIntensity = 0;
                    transitions.MaxIntensity = 0;
                    continue;
                }

                if (transitions.Progressive)
                    ProcessBinProgressive(bin, ref peaksAdded, ref maxTime, ref maxIntensity);
                else
                    ProcessBinSRM(bin, ref peaksAdded, ref maxTime, ref maxIntensity);
                
                transitions.MaxIntensity = Math.Max(transitions.MaxIntensity, maxIntensity);
            }

            // Rescale graph if necessary.
            if (_xMax <= X_AXIS_START)
            {
                _xMax = maxTime;
                _xAxisAnimation = new Animation(_graphPane.XAxis.Scale.Max, _xMax, 1, FAST_FRAME_MILLISECONDS);
            }
            else if (_xMax < maxTime)
            {
                _xMax = maxTime * 1.1;
                _xAxisAnimation = new Animation(_graphPane.XAxis.Scale.Max, _xMax, STEPS_FOR_TIME_AXIS_ANIMATION,
                    FAST_FRAME_MILLISECONDS);
            }

            if (_yMax < maxIntensity)
            {
                _yMax = maxIntensity*1.1;
                _yAxisAnimation = new Animation(_graphPane.YAxis.Scale.Max, _yMax, STEPS_FOR_INTENSITY_ANIMATION);
            }

            return peaksAdded;
        }

        private void ResetGraph()
        {
            string sampleName = _status.FilePath.GetSampleName();
            string filePath = _status.FilePath.GetFileName();
            var fileName = !string.IsNullOrEmpty(sampleName)
                ? string.Format(Resources.AsyncChromatogramsGraph_Render__0___sample__1_, filePath, sampleName)
                : filePath;
            _graphPane.Title.Text = fileName;
            _graphPane.CurveList.Clear();
            _graphPane.XAxis.Scale.Max = _xMax = Math.Max(X_AXIS_START, _status.Transitions.MaxRetentionTime);
            _graphPane.YAxis.Scale.Max = _yMax = Y_AXIS_START;
            _graphPane.AxisChange();
            _xAxisAnimation = null;
            _yAxisAnimation = null;
            _activeCurves.Clear();
            _lastCurve = null;
            _fullFrame = true;
            UpdateProgressLine(0);
            _graphPane.Chart.Fill = new Fill(Color.White);
        }

        private void ProcessBinProgressive(
            List<ChromatogramLoadingStatus.TransitionData.Peak> bin, 
            ref bool peaksAdded,
            ref float maxTime,
            ref float maxIntensity)
        {
            float retentionTime = bin[0].BinIndex*ChromatogramLoadingStatus.TIME_RESOLUTION;
            maxTime = _status.Transitions.MaxRetentionTime;

            // Order the top peaks to be shown for each bin.
            bin.Sort((a, b) => a.Intensity.CompareTo(b.Intensity));

            for (int i = 0; i < _activeCurves.Count; i++)
                _activeCurves[i].IsActive = false;

            // Add the top peaks to the list of active curves. This allows peaks to be displayed smoothly
            // and with anti-aliasing instead of as unconnected spikes.
            for (int i = bin.Count - 1; i >= 0; i--)
            {
                // Filter out small intensities.
                var peak = bin[i];
                float intensity = peak.Intensity;
                if (intensity < ChromatogramLoadingStatus.DISPLAY_FILTER_PERCENT*maxIntensity)
                    break;

                CurveInfo curve = null;
                foreach (var activeCurve in _activeCurves)
                {
                    if (ReferenceEquals(peak.ModifiedSequence, activeCurve.ModifiedSequence))
                    {
                        curve = activeCurve;
                        break;
                    }
                }

                // Add a new curve.
                if (curve == null)
                {
                    curve = new CurveInfo(bin[i].ModifiedSequence, bin[i].Color, retentionTime, intensity);
                    _graphPane.CurveList.Insert(0, curve.Curve);
                    _activeCurves.Add(curve);
                }
                // Add preceding zero if necessary.
                else if (curve.Curve.Points[curve.Curve.NPts - 1].X < retentionTime - ChromatogramLoadingStatus.TIME_RESOLUTION*1.01)
                {
                    curve.Curve.AddPoint(retentionTime - ChromatogramLoadingStatus.TIME_RESOLUTION, 0);
                    curve.Curve.AddPoint(retentionTime, intensity);
                    curve.Curve.AddPoint(retentionTime + ChromatogramLoadingStatus.TIME_RESOLUTION, 0);
                }
                // Add next point by modifying preceding intensity.
                else
                {
                    curve.Curve.Points[curve.Curve.NPts - 1].Y += intensity;
                    curve.Curve.AddPoint(retentionTime + ChromatogramLoadingStatus.TIME_RESOLUTION, 0);
                }

                // Add intensity value to the curve.
                maxIntensity = Math.Max(maxIntensity, intensity);
                curve.IsActive = true;
                peaksAdded = true;
            }

            // Remove curves that weren't among the top curves in this bin.
            for (int i = _activeCurves.Count - 1; i >= 0; i--)
            {
                if (!_activeCurves[i].IsActive)
                    _activeCurves.RemoveAt(i);
            }
        }

        private void ProcessBinSRM(
            List<ChromatogramLoadingStatus.TransitionData.Peak> bin,
            ref bool peaksAdded,
            ref float maxTime,
            ref float maxIntensity)
        {
            float retentionTime = bin[0].BinIndex * ChromatogramLoadingStatus.TIME_RESOLUTION;
            maxTime = Math.Max(maxTime, retentionTime);
            peaksAdded = true;

            foreach (var peak in bin)
            {
                // New peptide curve.
                if (_lastCurve == null || !ReferenceEquals(peak.ModifiedSequence, _lastCurve.ModifiedSequence))
                {
                    _lastCurve = new CurveInfo(peak.ModifiedSequence, peak.Color, retentionTime, peak.Intensity);
                    _graphPane.CurveList.Add(_lastCurve.Curve);
                    maxIntensity = Math.Max(maxIntensity, peak.Intensity);
                    continue;
                }

                // Add intensity to existing peptide curve.
                for (int i = _lastCurve.Curve.NPts - 1; i >= 0; i--)
                {
                    int binIndex = ChromatogramLoadingStatus.GetBinIndex((float) _lastCurve.Curve.Points[i].X);
                    if (binIndex > peak.BinIndex)
                    {
                        if (i == 0)
                        {
                            _lastCurve.InsertAt(0, retentionTime, peak.Intensity);
                            _lastCurve.CheckZeroes(0);
                            maxIntensity = Math.Max(maxIntensity, peak.Intensity);
                        }
                    }
                    else if (binIndex == peak.BinIndex)
                    {
                        _lastCurve.Curve.Points[i].Y += peak.Intensity;
                        _lastCurve.CheckZeroes(i);
                        maxIntensity = Math.Max(maxIntensity, (float) _lastCurve.Curve.Points[i].Y);
                        break;
                    }
                    else
                    {
                        _lastCurve.InsertAt(i + 1, retentionTime, peak.Intensity);
                        _lastCurve.CheckZeroes(i + 1);
                        maxIntensity = Math.Max(maxIntensity, peak.Intensity);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Perform one step of peak and graph axes animations.
        /// </summary>
        private void Animate()
        {
            FrameMilliseconds = NORMAL_FRAME_MILLISECONDS;

            // Animate range of x and y axes.
            if (_xAxisAnimation != null)
            {
                _graphPane.XAxis.Scale.Max = _xAxisAnimation.Step();
                _graphPane.AxisChange();

                if (_xAxisAnimation.Done)
                    _xAxisAnimation = null;
                else
                    FrameMilliseconds = FAST_FRAME_MILLISECONDS;
            }

            if (_yAxisAnimation != null)
            {
                _graphPane.YAxis.Scale.Max = _yAxisAnimation.Step();
                _graphPane.AxisChange();

                if (_yAxisAnimation.Done)
                    _yAxisAnimation = null;
                else
                    FrameMilliseconds = FAST_FRAME_MILLISECONDS;
            }
        }


        /// <summary>
        /// Update vertical line the marks current import time for progressively loaded files.
        /// </summary>
        private void UpdateProgressLine(double time)
        {
            // Remove old progressive loading indicators.
            if (_unfinishedBox != null)
            {
                _graphPane.GraphObjList.Remove(_unfinishedBox);
                _graphPane.GraphObjList.Remove(_unfinishedLine);
                _unfinishedBox = null;
            }

            // If we're still loading, create a white rectangle which blocks the fill background, indicating data yet to be loaded.
            if (time < _status.Transitions.MaxRetentionTime)
            {
                _graphPane.Chart.Fill = new Fill(_backgroundGradientColor1, _backgroundGradientColor2, 45.0f);
                _unfinishedBox = new BoxObj(
                    time,
                    _graphPane.YAxis.Scale.Max,
                    _graphPane.XAxis.Scale.Max - time,
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
                    time,
                    _graphPane.YAxis.Scale.Max,
                    time,
                    _graphPane.YAxis.Scale.Min)
                {
                    Location = { CoordinateFrame = CoordType.AxisXYScale },
                    Line = { Width = PROGRESS_LINE_WIDTH },
                    ZOrder = ZOrder.D_BehindAxis
                };
                _graphPane.GraphObjList.Add(_unfinishedLine);
            }
        }

        /// <summary>
        /// Associate one of our transition peaks (Peak) with ZedGraph's curve object (LineItem).
        /// </summary>
        private class CurveInfo
        {
            public LineItem Curve { get; private set; }
            public string ModifiedSequence { get; private set; }
            public bool IsActive { get; set; }

            public CurveInfo(string modifiedSequence, Color peptideColor, double retentionTime, float intensity)
            {
                Curve = new LineItem(string.Empty, new PointPairList(), peptideColor, SymbolType.None)
                {
                    Line = { Fill = new Fill(Color.FromArgb(80, peptideColor)), Width = 1, IsAntiAlias = true },
                    Label = { IsVisible = false }
                };
                Curve.AddPoint(retentionTime - ChromatogramLoadingStatus.TIME_RESOLUTION, 0);
                Curve.AddPoint(retentionTime, intensity);
                Curve.AddPoint(retentionTime + ChromatogramLoadingStatus.TIME_RESOLUTION, 0);
                ModifiedSequence = modifiedSequence;
            }

            public void InsertAt(int index, double retentionTime, double intensity)
            {
                Curve.AddPoint(0, 0);
                for (int j = Curve.NPts - 1; j > index; j--)
                {
                    Curve.Points[j].X = Curve.Points[j - 1].X;
                    Curve.Points[j].Y = Curve.Points[j - 1].Y;
                }
                Curve.Points[index].X = retentionTime;
                Curve.Points[index].Y = intensity;
            }

            public void CheckZeroes(int index)
            {
                if (index == 0 || Curve.Points[index].X - Curve.Points[index-1].X > ChromatogramLoadingStatus.TIME_RESOLUTION)
                {
                    InsertAt(index, Curve.Points[index].X - ChromatogramLoadingStatus.TIME_RESOLUTION, 0);
                    index++;
                }
                if (index == Curve.NPts - 1 || Curve.Points[index+1].X - Curve.Points[index].X > ChromatogramLoadingStatus.TIME_RESOLUTION)
                {
                    InsertAt(index+1, Curve.Points[index].X + ChromatogramLoadingStatus.TIME_RESOLUTION, 0);
                }
            }
        }
    }
}
