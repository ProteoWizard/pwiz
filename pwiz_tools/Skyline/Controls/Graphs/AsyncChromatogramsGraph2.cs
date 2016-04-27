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
using System.Windows.Forms;
using pwiz.MSGraph;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using ZedGraph;

namespace pwiz.Skyline.Controls.Graphs
{
    public partial class AsyncChromatogramsGraph2 : MSGraphControl
    {
        private const int NORMAL_TICKCOUNT = 3;
        private const int ANIMATE_TICKCOUNT = 1;
        private const int ANIMATE_UPDATERATE = 100;
        private const int STEPS_FOR_INTENSITY_ANIMATION = 6;    // half a second for growing peaks and adjusting intensity axis
        private const int STEPS_FOR_TIME_AXIS_ANIMATION = 10;   // one second for adjusting time axis

        private const float PROGRESS_LINE_WIDTH = 2.0f;         // width of line to show current progress for progressive graphs
        private const double X_AXIS_START = 1.0;                // initial value for time axis
        private const double Y_AXIS_START = 1.0;                // initial value for intensity axis

        private readonly Color _backgroundGradientColor1 = Color.FromArgb(240, 250, 250);
        private readonly Color _backgroundGradientColor2 = Color.FromArgb(250, 250, 210);
        private readonly Color _unfinishedLineColor = Color.FromArgb(170, 170, 170);
        private readonly Color _dimColor = Color.FromArgb(128, Color.LightGray);

        private readonly Dictionary<string, GraphInfo> _graphs = new Dictionary<string, GraphInfo>();
        private string _key;
        private int _tickCount = int.MaxValue;
        private bool _scaleIsLocked;
        private bool _fullRender;
        private readonly Animation _xAxisAnimation = new Animation(ANIMATE_UPDATERATE);
        private readonly Animation _yAxisAnimation = new Animation(ANIMATE_UPDATERATE);
        private readonly BoxObj _canceledBox;
        private readonly TextObj _canceledText;
        private double _renderMin;
        private double _renderMax;

        public AsyncChromatogramsGraph2()
        {
            InitializeComponent();
            timer.Interval = ANIMATE_UPDATERATE;
            timer.Tick += timer_Tick;

            GraphPane.Chart.Border.IsVisible = false;
            GraphPane.Chart.Fill.IsVisible = false;
            GraphPane.Chart.Fill = new Fill(_backgroundGradientColor1, _backgroundGradientColor2, 45.0f);
            GraphPane.Border.IsVisible = false;
            GraphPane.Title.IsVisible = true;

            GraphPane.XAxis.Title.Text = Resources.AsyncChromatogramsGraph_AsyncChromatogramsGraph_Retention_Time;
            GraphPane.XAxis.MinorTic.IsOpposite = false;
            GraphPane.XAxis.MajorTic.IsOpposite = false;
            GraphPane.XAxis.Scale.Min = 0.0;
            GraphPane.XAxis.Scale.Max = X_AXIS_START;

            GraphPane.YAxis.Title.Text = Resources.AsyncChromatogramsGraph_AsyncChromatogramsGraph_Intensity;
            GraphPane.YAxis.MinorTic.IsOpposite = false;
            GraphPane.YAxis.MajorTic.IsOpposite = false;
            GraphPane.YAxis.Scale.Min = 0.0;
            GraphPane.YAxis.Scale.Max = Y_AXIS_START;

            GraphHelper.FormatGraphPane(GraphPane);

            _canceledBox = new BoxObj(0, 0, 1, 1, _dimColor, _dimColor)
            {
                Location = { CoordinateFrame = CoordType.ChartFraction },
                ZOrder = ZOrder.D_BehindAxis
            };
            _canceledText = new TextObj(Resources.AsyncChromatogramsGraph2_AsyncChromatogramsGraph2_Canceled, 0.5, 0.5)
            {
                FontSpec = new FontSpec("Arial", 24, Color.Gray, true, false, false)    // Not L10N
                {
                    Border = new Border {IsVisible = false},
                    Fill = new Fill()
                },
                Location = { AlignH = AlignH.Center, AlignV = AlignV.Center, CoordinateFrame = CoordType.ChartFraction},
                ZOrder = ZOrder.A_InFront
            };
        }

        public string Key 
        {
            get { return _key; }
            set 
            {
                if (_key != value)
                {
                    _key = value;
                    NewGraph();
                    Render();
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
                    _fullRender = true;
                }
            }
        }

        private void timer_Tick(object sender, EventArgs e)
        {
            if (--_tickCount <= 0)
                Render();
        }

        private class GraphInfo
        {
            public string FileName;
            public CurveList Curves;
            public List<CurveInfo> ActiveCurves;
            public CurveInfo LastCurve;
            public float MaxX;
            public float MaxY;
            public float? CurrentTime;
        }

        private void Render()
        {
            lock (_graphs)
            {
                // No graph to render yet.
                GraphInfo info;
                if (Key == null || !_graphs.TryGetValue(Key, out info))
                {
                    _tickCount = int.MaxValue;
                    return;
                }

                // Scale axis depending on whether the axes are locked between graphs.
                float maxX = info.MaxX;
                float maxY = info.MaxY;
                if (ScaleIsLocked)
                {
                    foreach (var pair in _graphs)
                    {
                        maxX = Math.Max(maxX, pair.Value.MaxX);
                        maxY = Math.Max(maxY, pair.Value.MaxY);
                    }
                }

                if (maxY == 0.0)
                    return;

                var graphPane = GraphPane;
                graphPane.Title.Text = info.FileName;
                graphPane.CurveList = info.Curves;

                _xAxisAnimation.SetTarget(graphPane.XAxis.Scale.Max, maxX, STEPS_FOR_TIME_AXIS_ANIMATION);
                _yAxisAnimation.SetTarget(graphPane.YAxis.Scale.Max, maxY*1.1, STEPS_FOR_INTENSITY_ANIMATION);

                if (_xAxisAnimation.IsActive || _yAxisAnimation.IsActive)
                {
                    _fullRender = true;
                    graphPane.XAxis.Scale.Max = _xAxisAnimation.Step();
                    graphPane.YAxis.Scale.Max = _yAxisAnimation.Step();
                    AxisChange();
                }

                ShowUnfinishedLine(info.CurrentTime);

                if (_fullRender)
                {
                    _fullRender = false;
                    Invalidate();
                }
                else if (_renderMax > _renderMin)
                {
                    // Render incremental changes to current graph.
                    var p1 = graphPane.GeneralTransform(_renderMin, 0, CoordType.AxisXYScale);
                    var p2 = graphPane.GeneralTransform(_renderMax, 0, CoordType.AxisXYScale);
                    int x = (int)p1.X - 1;
                    int y = 0;
                    int width = (int)(p2.X + PROGRESS_LINE_WIDTH) - x + 2;
                    int height = (int)p1.Y + 2;
                    Invalidate(new Rectangle(x, y, width, height));
                }

                if (info.CurrentTime.HasValue)
                    _renderMin = _renderMax = info.CurrentTime.Value;
            }

            Update();
            _tickCount = _xAxisAnimation.IsActive || _yAxisAnimation.IsActive ? ANIMATE_TICKCOUNT : NORMAL_TICKCOUNT;
        }

        private void NewGraph()
        {
            GraphPane.GraphObjList.Clear();
            _fullRender = true;
        }

        private void ShowUnfinishedLine(float? currentTime)
        {
            if (IsCanceled)
            {
                NewGraph();
                GraphPane.GraphObjList.Add(_canceledBox);
                GraphPane.GraphObjList.Add(_canceledText);
            }
            else if (GraphPane.YAxis.Scale.Max > GraphPane.YAxis.Scale.Min &&
                currentTime.HasValue && currentTime.Value < GraphPane.XAxis.Scale.Max * 0.95)
            {
                if (GraphPane.GraphObjList.Count == 0)
                    _fullRender = true;
                else
                {
                    GraphPane.GraphObjList.Clear();
                    _renderMax = currentTime.Value;
                }

                var unfinishedBox = new BoxObj(
                    currentTime.Value,
                    GraphPane.YAxis.Scale.Max,
                    GraphPane.XAxis.Scale.Max - currentTime.Value,
                    GraphPane.YAxis.Scale.Max - GraphPane.YAxis.Scale.Min,
                    Color.White, Color.White)
                {
                    Location = {CoordinateFrame = CoordType.AxisXYScale},
                    ZOrder = ZOrder.F_BehindGrid
                };

                var unfinishedLine = new LineObj(
                    _unfinishedLineColor,
                    currentTime.Value,
                    GraphPane.YAxis.Scale.Max,
                    currentTime.Value,
                    GraphPane.YAxis.Scale.Min)
                {
                    Location = {CoordinateFrame = CoordType.AxisXYScale},
                    Line = {Width = PROGRESS_LINE_WIDTH},
                    ZOrder = ZOrder.D_BehindAxis
                };

                GraphPane.GraphObjList.Add(unfinishedBox);
                GraphPane.GraphObjList.Add(unfinishedLine);
            }
            else
            {
                NewGraph();
            }
        }

        public void ClearGraph(MsDataFileUri filePath)
        {
            lock (_graphs)
            {
                _graphs.Remove(filePath.GetFilePath());
            }
        }

        /// <summary>
        /// Update status.
        /// </summary>
        public void UpdateStatus(ChromatogramLoadingStatus status)
        {
            lock (_graphs)
            {
                // Create info for new file.
                var key = status.FilePath.GetFilePath();
                GraphInfo info;
                if (!_graphs.TryGetValue(key, out info))
                {
                    info = _graphs[key] = new GraphInfo
                    {
                        FileName = status.FilePath.GetFileNameWithoutExtension(),
                        Curves = new CurveList(),
                        ActiveCurves = new List<CurveInfo>()
                    };
                }

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
                _tickCount = 0;
            }
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
                    info.Curves.Insert(0, curve.Curve);
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
                    info.Curves.Add(info.LastCurve.Curve);
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
            public string ModifiedSequence { get; private set; }
            public bool IsActive { get; set; }

            public CurveInfo(string modifiedSequence, Color peptideColor, double retentionTime, float intensity)
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

        private void AsyncChromatogramsGraph2_ContextMenuBuilder(ZedGraphControl sender, ContextMenuStrip menuStrip, Point mousePt, ContextMenuObjectState objState)
        {
            ZedGraphHelper.BuildContextMenu(sender, menuStrip);
        }
    }
}
