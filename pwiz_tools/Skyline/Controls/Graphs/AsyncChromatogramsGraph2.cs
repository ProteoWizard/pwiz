/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2016 University of Washington - Seattle, WA
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
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using ZedGraph;

namespace pwiz.Skyline.Controls.Graphs
{
    public partial class AsyncChromatogramsGraph2 : AsyncRenderControl
    {
        private const int ANIMATE_UPDATERATE = 100;             // animate 10 frames/sec.
        private const int STEPS_FOR_INTENSITY_ANIMATION = 6;    // half a second for growing peaks and adjusting intensity axis
        private const int STEPS_FOR_TIME_AXIS_ANIMATION = 10;   // one second for adjusting time axis
        private const int MIN_INCREMENTAL_RENDER = 1000;        // one second for non-aminating advances
        private const int MAX_INCREMENTAL_RENDER = 5000;        // five seconds to render a single shift before smaller increment is allowed
        private const int INCREMENTS_PER_PASS = 100;            // one hundred incremental shifts across the x-axis

        private const float PROGRESS_LINE_WIDTH = 2.0f;         // width of line to show current progress for progressive graphs
        private const double X_AXIS_START = 1.0;                // initial value for time axis
        private const double Y_AXIS_START = 1.0;                // initial value for intensity axis

        private readonly Color _backgroundGradientColor1 = Color.FromArgb(240, 250, 250);
        private readonly Color _backgroundGradientColor2 = Color.FromArgb(250, 250, 210);
        private readonly Color _unfinishedLineColor = Color.FromArgb(170, 170, 170);
        private readonly Color _dimColor = Color.FromArgb(128, Color.LightGray);

        // Hold rendering information for each graph
        private class GraphInfo
        {
            public GraphPane GraphPane;
            public List<CurveInfo> ActiveCurves;
            public CurveInfo LastCurve;
            public float MaxX;
            public float MaxY;
            public float? CurrentTime;
        }

        private GraphPane _templatePane;
        private GraphPane _graphPane;
        private BoxObj _canceledBox;
        private TextObj _canceledText;
        private readonly Dictionary<string, GraphInfo> _graphs = new Dictionary<string, GraphInfo>();
        private string _key;
        private bool _scaleIsLocked;
        private readonly Animation _xAxisAnimation = new Animation(ANIMATE_UPDATERATE);
        private readonly Animation _yAxisAnimation = new Animation(ANIMATE_UPDATERATE);
        private double _renderMin;
        private double _renderMax;
        private double _lastTime;
        private DateTime _lastRender;
        private bool _backgroundInitialized;

        public AsyncChromatogramsGraph2()
            : base(@"AllChromatograms background render")
        {
            InitializeComponent();
        }

        /// <summary>
        /// Start animation timer on load.
        /// </summary>
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            if (DesignMode) return;

            _lastRender = DateTime.UtcNow; // Said to be 117x faster than Now and this is for a delta

            timer.Interval = ANIMATE_UPDATERATE;
            timer.Tick += timer_Tick;
        }

        /// <summary>
        /// Initialize graph renderer on the background thread.
        /// </summary>
        private void BackgroundInitialize()
        {
            // The template pane is a blank graph that will be cloned to create a graph for each imported file.
            _templatePane = new GraphPane();
            _templatePane.Chart.Border.IsVisible = false;
            _templatePane.Chart.Fill.IsVisible = false;
            _templatePane.Chart.Fill = new Fill(_backgroundGradientColor1, _backgroundGradientColor2, 45.0f);
            _templatePane.Border.IsVisible = false;
            _templatePane.Title.IsVisible = true;

            _templatePane.XAxis.Title.Text = Resources.AsyncChromatogramsGraph_AsyncChromatogramsGraph_Retention_Time;
            _templatePane.XAxis.MinorTic.IsOpposite = false;
            _templatePane.XAxis.MajorTic.IsOpposite = false;
            _templatePane.XAxis.Scale.Min = 0.0;
            _templatePane.XAxis.Scale.Max = X_AXIS_START;

            _templatePane.YAxis.Title.Text = Resources.AsyncChromatogramsGraph_AsyncChromatogramsGraph_Intensity;
            _templatePane.YAxis.MinorTic.IsOpposite = false;
            _templatePane.YAxis.MajorTic.IsOpposite = false;
            _templatePane.YAxis.Scale.Min = 0.0;
            _templatePane.YAxis.Scale.Max = Y_AXIS_START;

            GraphHelper.FormatGraphPane(_templatePane);

            _canceledBox = new BoxObj(0, 0, 1, 1, _dimColor, _dimColor)
            {
                Location = { CoordinateFrame = CoordType.ChartFraction },
                ZOrder = ZOrder.D_BehindAxis
            };
            _canceledText = new TextObj(Resources.AsyncChromatogramsGraph2_AsyncChromatogramsGraph2_Canceled, 0.5, 0.5)
            {
                FontSpec = new FontSpec(@"Arial", 24, Color.Gray, true, false, false)
                {
                    Border = new Border { IsVisible = false },
                    Fill = new Fill()
                },
                Location = { AlignH = AlignH.Center, AlignV = AlignV.Center, CoordinateFrame = CoordType.ChartFraction },
                ZOrder = ZOrder.A_InFront
            };
        }

        /// <summary>
        /// Update status (main thread).
        /// </summary>
        public void UpdateStatus(ChromatogramLoadingStatus status)
        {
            if (!_backgroundInitialized)
            {
                _backgroundInitialized = true;
                BackgroundInitialize();
            }

            // Create info for new file.
            var key = status.FilePath.GetFilePath();
            var info = GetInfo(key);
            if (info == null)
            {
                info = _graphs[key] = new GraphInfo
                {
                    GraphPane = _templatePane.Clone(),
                    ActiveCurves = new List<CurveInfo>()
                };
                info.GraphPane.Title.Text = status.FilePath.GetFileNameWithoutExtension();
            }

            // Create curve information from the transition data.
            List<ChromatogramLoadingStatus.TransitionData.Peak> bin;
            while (status.Transitions.BinnedPeaks.TryDequeue(out bin))
            {
                if (status.Transitions.Progressive)
                    ProcessBinProgressive(bin, info);
                else
                    ProcessBinSRM(bin, info);
            }

            if (status.Transitions.Progressive)
            {
                info.CurrentTime = status.Transitions.CurrentTime;
                info.MaxX = Math.Max(info.MaxX, status.Transitions.MaxRetentionTime);
            }
            else
            {
                info.CurrentTime = null;
            }
            status.Transitions.MaxIntensity = info.MaxY;
        }

        /// <summary>
        /// This is the rendering loop, called periodically to update the graph.
        /// </summary>
        private void timer_Tick(object sender, EventArgs e)
        {
            var info = GetInfo(Key);
            if (info == null)
                return;

            // Find new maximum values on x and y axes.
            float maxX, maxY;
            Rescale(info, out maxX, out maxY);
            if (maxY == 0.0)
                return;

            // Start scaling animation if necessary.
            _xAxisAnimation.SetTarget(info.GraphPane.XAxis.Scale.Max, maxX, STEPS_FOR_TIME_AXIS_ANIMATION);
            _yAxisAnimation.SetTarget(info.GraphPane.YAxis.Scale.Max, maxY * 1.1, STEPS_FOR_INTENSITY_ANIMATION);

            // Tell Zedgraph if axes are being changed.
            double nextTime = info.CurrentTime ?? 0;
            double millisElapsed = (DateTime.UtcNow - _lastRender).TotalMilliseconds;
            if (_xAxisAnimation.IsActive || _yAxisAnimation.IsActive)
            {
                info.GraphPane.XAxis.Scale.Max = _xAxisAnimation.Step();
                info.GraphPane.YAxis.Scale.Max = _yAxisAnimation.Step();
                info.GraphPane.AxisChange();
                StartRender();
            }
            else if (millisElapsed < MIN_INCREMENTAL_RENDER)
            {
                return;
            }
            else
            {
                double minTime = Math.Min(_renderMin, _lastTime);
                double maxTime = Math.Max(_renderMax, nextTime);
                
                // Limit the number of steps for advancing progress
                if (millisElapsed < MAX_INCREMENTAL_RENDER && maxTime - minTime <= info.GraphPane.XAxis.Scale.Max/INCREMENTS_PER_PASS)
                    return;

                IncrementalRender(info, minTime, maxTime);
                _lastRender = DateTime.UtcNow;
            }

            _lastTime = nextTime;
            _renderMin = double.MaxValue;
            _renderMax = double.MinValue;
        }

        private void IncrementalRender(GraphInfo info, double minX, double maxX)
        {
            // Render incremental changes to current graph.
            var p1 = info.GraphPane.GeneralTransform(minX, 0, CoordType.AxisXYScale);
            var p2 = info.GraphPane.GeneralTransform(maxX, 0, CoordType.AxisXYScale);
            int x = (int)p1.X - 1;
            int width = (int)(p2.X + PROGRESS_LINE_WIDTH) - x + 2;
            StartRender(new Rectangle(x, 0, width, Height));
            // TODO(bspratt): there's still an issue with the clip rect in X - I don't
            // always see the grey "incomplete" bar on my system (the SVN 9985 version of
            // AgilentSpectrumMillIMSImportTest() is a reliable repro.  It has to do with
            // adding that verticle bar after the clip region has been established.  It would
            // be better to do the work of copying the display list here instead of later in
            // CopyState() so the end time stays synched.
        }

        /// <summary>
        /// Determine maximum values for x and y axes, possibly across all importing files.
        /// </summary>
        private void Rescale(GraphInfo info, out float maxX, out float maxY)
        {
            // Scale axis depending on whether the axes are locked between graphs.
            maxX = info.MaxX;
            maxY = info.MaxY;
            if (ScaleIsLocked)
            {
                foreach (var pair in _graphs)
                {
                    maxX = Math.Max(maxX, pair.Value.MaxX);
                    maxY = Math.Max(maxY, pair.Value.MaxY);
                }
            }
        }

        /// <summary>
        /// Copy graphics on main thread to freeze them for background rendering.
        /// </summary>
        protected override void CopyState()
        {
            var info = GetInfo(Key);
            if (info != null)
            {
                _graphPane = info.GraphPane.Clone();
                AddUnfinishedLine(_graphPane, info.CurrentTime);
            }
        }

        /// <summary>
        /// Render content to a bitmap.
        /// </summary>
        protected override void Render(Bitmap bitmap, Rectangle renderRect)
        {
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.SetClip(renderRect);
                _graphPane.ReSize(graphics, new RectangleF(0, 0, bitmap.Width, bitmap.Height));
                _graphPane.Draw(graphics);
            }
        }

        public string Key 
        {
            get { return _key; }
            set 
            {
                if (_key != value)
                {
                    _key = value;
                    Redraw();
                }
            }
        }

        public bool IsCanceled { get; set; }

        public bool ScaleIsLocked
        {
            get { return _scaleIsLocked; }
            set
            {
                if (_scaleIsLocked != value)
                {
                    _scaleIsLocked = value;
                    Redraw();
                }
            }
        }

        /// <summary>
        /// Redraw a graph entirely when we switch between graphs.
        /// </summary>
        public void Redraw()
        {
            _renderMin = double.MaxValue;
            _renderMax = double.MinValue;
            var info = GetInfo(Key);
            if (info != null)
            {
                float maxX, maxY;
                Rescale(info, out maxX, out maxY);
                info.GraphPane.XAxis.Scale.Max = maxX;
                info.GraphPane.YAxis.Scale.Max = maxY * 1.1;
                info.GraphPane.AxisChange();
                StartRender();
            }
        }

        private GraphInfo GetInfo(string key)
        {
            return key != null && _graphs.ContainsKey(key) ? _graphs[key] : null;
        }

        private void AddUnfinishedLine(GraphPane graphPane, float? currentTime)
        {
            if (IsCanceled)
            {
                graphPane.GraphObjList.Add(_canceledBox);
                graphPane.GraphObjList.Add(_canceledText);
            }
            else if (graphPane.YAxis.Scale.Max > graphPane.YAxis.Scale.Min &&
                currentTime.HasValue && currentTime.Value < graphPane.XAxis.Scale.Max * 0.95)
            {
                var unfinishedBox = new BoxObj(
                    currentTime.Value,
                    graphPane.YAxis.Scale.Max,
                    graphPane.XAxis.Scale.Max - currentTime.Value,
                    graphPane.YAxis.Scale.Max - graphPane.YAxis.Scale.Min,
                    Color.White, Color.White)
                {
                    Location = {CoordinateFrame = CoordType.AxisXYScale},
                    ZOrder = ZOrder.F_BehindGrid
                };

                var unfinishedLine = new LineObj(
                    _unfinishedLineColor,
                    currentTime.Value,
                    graphPane.YAxis.Scale.Max,
                    currentTime.Value,
                    graphPane.YAxis.Scale.Min)
                {
                    Location = {CoordinateFrame = CoordType.AxisXYScale},
                    Line = {Width = PROGRESS_LINE_WIDTH},
                    ZOrder = ZOrder.D_BehindAxis
                };

                graphPane.GraphObjList.Add(unfinishedBox);
                graphPane.GraphObjList.Add(unfinishedLine);
            }
            else
            {
                graphPane.GraphObjList.Clear();
            }
        }

        public void ClearGraph(MsDataFileUri filePath)
        {
            _graphs.Remove(filePath.GetFilePath());
        }

        private void ProcessBinProgressive(
            List<ChromatogramLoadingStatus.TransitionData.Peak> bin,
            GraphInfo info)
        {
            float retentionTime = bin[0].BinIndex * ChromatogramLoadingStatus.TIME_RESOLUTION;
            info.MaxX = Math.Max(info.MaxX, retentionTime + ChromatogramLoadingStatus.TIME_RESOLUTION);
            _renderMin = Math.Min(_renderMin, retentionTime - ChromatogramLoadingStatus.TIME_RESOLUTION);
            _renderMax = Math.Max(_renderMax, retentionTime + ChromatogramLoadingStatus.TIME_RESOLUTION);

            // Order the top peaks to be shown for each bin.
            bin.Sort((a, b) => a.Intensity.CompareTo(b.Intensity));

            for (int i = 0; i < info.ActiveCurves.Count; i++)
                info.ActiveCurves[i].IsActive = false;

            // Add the top peaks to the list of active curves. This allows peaks to be displayed smoothly
            // and with anti-aliasing instead of as unconnected spikes.
            for (int i = bin.Count - 1; i >= 0; i--)
            {
                // Filter out small intensities.
                var peak = bin[i];
                float intensity = peak.Intensity;
                if (intensity < ChromatogramLoadingStatus.DISPLAY_FILTER_PERCENT * info.MaxY)
                    break;
                info.MaxY = Math.Max(info.MaxY, intensity);

                CurveInfo curve = null;
                foreach (var activeCurve in info.ActiveCurves)
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
                    info.GraphPane.CurveList.Insert(0, curve.Curve);
                    info.ActiveCurves.Add(curve);
                }
                // Add preceding zero if necessary.
                else if (curve.Curve.Points[curve.Curve.NPts - 1].X < retentionTime - ChromatogramLoadingStatus.TIME_RESOLUTION * 1.01)
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

                curve.IsActive = true;
            }

            // Remove curves that weren't among the top curves in this bin.
            for (int i = info.ActiveCurves.Count - 1; i >= 0; i--)
            {
                if (!info.ActiveCurves[i].IsActive)
                    info.ActiveCurves.RemoveAt(i);
            }
        }

        private void ProcessBinSRM(
            List<ChromatogramLoadingStatus.TransitionData.Peak> bin,
            GraphInfo info)
        {
            float retentionTime = bin[0].BinIndex * ChromatogramLoadingStatus.TIME_RESOLUTION;
            info.MaxX = Math.Max(info.MaxX, retentionTime + ChromatogramLoadingStatus.TIME_RESOLUTION);
            _renderMin = Math.Min(_renderMin, retentionTime - ChromatogramLoadingStatus.TIME_RESOLUTION);
            _renderMax = Math.Max(_renderMax, retentionTime + ChromatogramLoadingStatus.TIME_RESOLUTION);

            foreach (var peak in bin)
            {
                float intensity = peak.Intensity;
                info.MaxY = Math.Max(info.MaxY, intensity);

                // New peptide curve.
                if (info.LastCurve == null || !ReferenceEquals(peak.ModifiedSequence, info.LastCurve.ModifiedSequence))
                {
                    info.LastCurve = new CurveInfo(peak.ModifiedSequence, peak.Color, retentionTime, intensity);
                    info.GraphPane.CurveList.Add(info.LastCurve.Curve);
                    continue;
                }

                // Add intensity to existing peptide curve.
                for (int i = info.LastCurve.Curve.NPts - 1; i >= 0; i--)
                {
                    int binIndex = ChromatogramLoadingStatus.GetBinIndex((float)info.LastCurve.Curve.Points[i].X);
                    if (binIndex > peak.BinIndex)
                    {
                        if (i == 0)
                        {
                            info.LastCurve.InsertAt(0, retentionTime, intensity);
                            info.LastCurve.CheckZeroes(0);
                        }
                    }
                    else if (binIndex == peak.BinIndex)
                    {
                        info.LastCurve.Curve.Points[i].Y += intensity;
                        info.MaxY = Math.Max(info.MaxY, (float)info.LastCurve.Curve.Points[i].Y);
                        info.LastCurve.CheckZeroes(i);
                        break;
                    }
                    else
                    {
                        info.LastCurve.InsertAt(i + 1, retentionTime, intensity);
                        info.LastCurve.CheckZeroes(i + 1);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Associate one of our transition peaks (Peak) with ZedGraph's curve object (LineItem).
        /// </summary>
        private class CurveInfo
        {
            public LineItem Curve { get; private set; }
            public Target ModifiedSequence { get; private set; }
            public bool IsActive { get; set; }

            public CurveInfo(Target modifiedSequence, Color peptideColor, double retentionTime, float intensity)
            {
                var fillColor = Color.FromArgb(
                    175 + 80 * peptideColor.R / 255,
                    175 + 80 * peptideColor.G / 255,
                    175 + 80 * peptideColor.B / 255);
                Curve = new LineItem(string.Empty, new PointPairList(), peptideColor, SymbolType.None)
                {
                    Line = { Fill = new Fill(fillColor), Width = 1, IsAntiAlias = true },
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
                if (index == 0 || Curve.Points[index].X - Curve.Points[index - 1].X > ChromatogramLoadingStatus.TIME_RESOLUTION)
                {
                    InsertAt(index, Curve.Points[index].X - ChromatogramLoadingStatus.TIME_RESOLUTION, 0);
                    index++;
                }
                if (index == Curve.NPts - 1 || Curve.Points[index + 1].X - Curve.Points[index].X > ChromatogramLoadingStatus.TIME_RESOLUTION)
                {
                    InsertAt(index + 1, Curve.Points[index].X + ChromatogramLoadingStatus.TIME_RESOLUTION, 0);
                }
            }
        }
    }
}
