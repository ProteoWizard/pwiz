//
// $Id: GraphForm.cs 2321 2010-10-21 20:26:30Z chambm $
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2009 Vanderbilt University - Nashville, TN 37232
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
// 
// http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using DigitalRune.Windows.Docking;
using pwiz.MSGraph;
using ZedGraph;

using System.Diagnostics;
using System.Linq;
using System.Runtime.Caching.Generic;
using JWC;
using pwiz.CLI.cv;
using pwiz.CLI.msdata;
using pwiz.Common.Collections;
using SpyTools;

namespace seems
{
    public partial class HeatmapForm : ManagedDockableForm
    {
        public pwiz.MSGraph.MSGraphControl ZedGraphControl { get { return msGraphControl; } }

        private class BoundingBox
        {
            public double MinX { get; set; }
            public double MinY { get; set; }
            public double MaxX { get; set; }
            public double MaxY { get; set; }
        }

        private struct MobilityData
        {
            public MobilityData(SpectrumList spectrumList, double scanTime, int startIndex, int endIndex) : this()
            {
                SpectrumList = spectrumList;
                ScanTime = scanTime;
                StartIndex = startIndex;
                EndIndex = endIndex;
            }

            public SpectrumList SpectrumList { get; private set; }
            public double ScanTime { get; private set; }
            public int StartIndex { get; private set; }
            public int EndIndex { get; private set; }

            public Tuple<List<Point3D>, BoundingBox> Get3dSpectrum()
            {
                var result = new Tuple<List<Point3D>, BoundingBox>(new List<Point3D>(),
                    new BoundingBox
                    {
                        MinX = Double.MaxValue, MaxX = Double.MinValue, MinY = Double.MaxValue, MaxY = Double.MinValue
                    });

                var bounds = result.Item2;

                if (StartIndex == EndIndex)
                {
                    var s = SpectrumList.spectrum(StartIndex, true);
                    var mzArray = s.getMZArray().data.Storage();
                    var intensityArray = s.getIntensityArray().data.Storage();
                    var mobilityArray = s.GetIonMobilityArray();

                    if (mobilityArray == null)
                        throw new NullReferenceException("mobilityBDA");

                    for (int j = 0, end = mzArray.Length; j < end; ++j)
                    {
                        double mz = mzArray[j];
                        double intensity = intensityArray[j];
                        double mobility = mobilityArray[j];

                        bounds.MinX = Math.Min(bounds.MinX, mz);
                        bounds.MinY = Math.Min(bounds.MinY, mobility);
                        bounds.MaxX = Math.Max(bounds.MaxX, mz);
                        bounds.MaxY = Math.Max(bounds.MaxY, mobility);

                        result.Item1.Add(new Point3D(mz, mobility, intensity));
                    }
                }
                else
                {
                    for (int i = StartIndex, end = EndIndex; i <= end; ++i)
                    {
                        var s = SpectrumList.spectrum(i, true);
                        var mzArray = s.getMZArray().data.Storage();
                        var intensityArray = s.getIntensityArray().data.Storage();
                        var mobilityArray = s.GetIonMobilityArray();
                        for (int j = 0; j < mzArray.Length; ++j)
                        {
                            double mz = mzArray[j];
                            double intensity = intensityArray[j];
                            double mobility = mobilityArray?.ElementAt(j) ?? (double) s.scanList.scans[0].cvParamChild(CVID.MS_ion_mobility_attribute).value;

                            bounds.MinX = Math.Min(bounds.MinX, mz);
                            bounds.MinY = Math.Min(bounds.MinY, mobility);
                            bounds.MaxX = Math.Max(bounds.MaxX, mz);
                            bounds.MaxY = Math.Max(bounds.MaxY, mobility);

                            result.Item1.Add(new Point3D(mz, mobility, intensity));
                        }
                    }
                }

                return result;
            }
        }

        private class ChromatogramControl : IMSGraphItemExtended
        {
            public ChromatogramControl(ManagedDataSource source, int targetMsLevel, GraphPane graphPane)
            {
                Title = String.Format("TIC Chromatogram (ms{0})", targetMsLevel);

                var dgv = source.SpectrumListForm.GridView;
                var scanTimeColumn = dgv.Columns["ScanTime"];
                var ticColumn = dgv.Columns["TotalIonCurrent"];
                var msLevelColumn = dgv.Columns["MsLevel"];
                var ionMobilityColumn = dgv.Columns["IonMobility"];
                var dataPointsColumn = dgv.Columns["DataPoints"];

                IntensityByScanTime = new SortedList<double, double>(dgv.RowCount / 200);

                for (int i = 0; i < dgv.RowCount; ++i)
                {
                    int msLevel = (int) dgv[msLevelColumn.Index, i].Value;
                    if (targetMsLevel != msLevel || Convert.ToInt32(dgv[dataPointsColumn.Index, i].Value) == 0)
                        continue;

                    double scanTime = (double) dgv[scanTimeColumn.Index, i].Value;
                    double intensity = (double) dgv[ticColumn.Index, i].Value;

                    if (!IntensityByScanTime.ContainsKey(scanTime))
                        IntensityByScanTime[scanTime] = intensity;
                    else
                        IntensityByScanTime[scanTime] += intensity;
                }

                Points = new PointPairList(IntensityByScanTime.Keys, IntensityByScanTime.Values);

                Source = source;
                MsLevel = targetMsLevel;
                GraphPane = graphPane;
                SelectedTime = 0;
            }

            public MSGraphItemType GraphItemType { get {return MSGraphItemType.chromatogram;} }
            public MSGraphItemDrawMethod GraphItemDrawMethod { get {return MSGraphItemDrawMethod.line;} }

            public string Title { get; private set; }

            public Color Color { get { return Color.Gray; } }
            public float LineWidth { get { return ZedGraph.LineBase.Default.Width; } }

            public void CustomizeXAxis(Axis axis)
            {
                axis.Title.IsVisible = false;
            }

            public void CustomizeYAxis(Axis axis)
            {
                axis.Title.FontSpec.Family = "Arial";
                axis.Title.FontSpec.Size = 14;
                axis.Color = axis.Title.FontSpec.FontColor = Color.Black;
                axis.Title.FontSpec.Border.IsVisible = false;
                axis.Title.Text = "Intensity";
            }

            public PointAnnotation AnnotatePoint(PointPair point)
            {
                return new PointAnnotation();
            }

            public void AddAnnotations(MSGraphPane graphPane, Graphics g, MSPointList pointList, GraphObjList annotations)
            {
            }

            public void CustomizeCurve(CurveItem curveItem)
            {
                var line = curveItem as LineItem;
                if (line != null) line.Symbol = new Symbol(SymbolType.Circle, Color) { Fill = new Fill(new SolidBrush(Color)) };
            }

            public void AddPreCurveAnnotations(MSGraphPane graphPane, Graphics g, MSPointList pointList, GraphObjList annotations)
            {
            }

            public void SelectTime(double scanTime)
            {
                int index = IntensityByScanTime.IndexOfKey(scanTime);
                if (index < 0)
                    throw new ArgumentOutOfRangeException("scanTime", "time point not found in chromatogram");

                GraphPane.GraphObjList.RemoveAll(o => o is LineObj);
                GraphPane.GraphObjList.Add(new LineObj {Location = new Location(scanTime, 0, 0, 1, CoordType.XScaleYChartFraction, AlignH.Center, AlignV.Center), Line = new Line(Color.PaleVioletRed) { Width = 2 } });
                SelectedTime = scanTime;
            }

            public SortedList<double, double> IntensityByScanTime { get; private set; }
            public IPointList Points { get; private set; }

            public ManagedDataSource Source { get; private set; }
            public int MsLevel { get; private set; }
            public GraphPane GraphPane { get; private set; }
            public double SelectedTime { get; private set; }
        }

        private List<ChromatogramControl> ticChromatogramByMsLevel; // 1 chromatogram per ms level
        private List<HeatMapGraphPane> heatmapGraphPaneByMsLevel; // 1 heatmap per ms level
        private List<Map<double, MobilityData>> ionMobilityBinsByMsLevelAndScanTime;
        private MemoryCache<MobilityData, Tuple<List<Point3D>, BoundingBox>> heatmapPointsCache;
        private Map<double, List<string>> isolationMzByScanTime;

        public HeatmapForm(Manager manager, ManagedDataSource source)
        {
            InitializeComponent();

            Manager = manager;
            Source = source;
            heatmapGraphPaneByMsLevel = new List<HeatMapGraphPane>();
            ticChromatogramByMsLevel = new List<ChromatogramControl>();
            ionMobilityBinsByMsLevelAndScanTime = new List<Map<double, MobilityData>>();
            isolationMzByScanTime = new Map<double, List<string>>();

            msGraphControl.BorderStyle = BorderStyle.None;

            msGraphControl.MasterPane.InnerPaneGap = 1;
            msGraphControl.MouseDownEvent += msGraphControl_MouseDownEvent;
            msGraphControl.MouseUpEvent += msGraphControl_MouseUpEvent;
            msGraphControl.MouseMoveEvent += msGraphControl_MouseMoveEvent;

            msGraphControl.ZoomButtons = MouseButtons.Left;
            msGraphControl.ZoomModifierKeys = Keys.None;
            msGraphControl.ZoomButtons2 = MouseButtons.None;

            msGraphControl.UnzoomButtons = new MSGraphControl.MouseButtonClicks( MouseButtons.Middle );
            msGraphControl.UnzoomModifierKeys = Keys.None;
            msGraphControl.UnzoomButtons2 = new MSGraphControl.MouseButtonClicks( MouseButtons.None );

            msGraphControl.UnzoomAllButtons = new MSGraphControl.MouseButtonClicks( MouseButtons.Left, 2 );
            msGraphControl.UnzoomAllButtons2 = new MSGraphControl.MouseButtonClicks( MouseButtons.None );

            msGraphControl.PanButtons = MouseButtons.Left;
            msGraphControl.PanModifierKeys = Keys.Control;
            msGraphControl.PanButtons2 = MouseButtons.None;

            msGraphControl.ZoomEvent += msGraphControl_ZoomEvent;
            msGraphControl.IsEnableVZoom = msGraphControl.IsEnableVPan = true;

            Text = TabText = source.Source.Name + " Ion Mobility Heatmaps";

            ContextMenuStrip dummyMenu = new ContextMenuStrip();
            dummyMenu.Opening += foo_Opening;
            TabPageContextMenuStrip = dummyMenu;
        }

        private void setScale(Scale scale, double min, double max, bool isZoom=false)
        {
            double scaleFactor = Math.Pow(10, Math.Floor(Math.Log10(max - min))) / 10;
            if (isZoom)
            {
                scale.Min = Math.Max(scale.Min, Math.Floor(min / scaleFactor) * scaleFactor);
                scale.Max = Math.Min(scale.Max, Math.Ceiling(max / scaleFactor) * scaleFactor);
            }
            else
            {
                scale.Min = Math.Floor(min / scaleFactor) * scaleFactor;
                scale.Max = Math.Ceiling(max / scaleFactor) * scaleFactor;
            }
        }

        void msGraphControl_ZoomEvent(ZedGraphControl sender, ZoomState oldState, ZoomState newState, PointF mousePosition)
        {
            /*for (int i = 0; i < heatmapGraphPaneByMsLevel.Count; ++i)
            {
                var bounds = heatmapBoundsByMsLevel[i];
                setScale(heatmapGraphPaneByMsLevel[i].XAxis.Scale, bounds.MinX, bounds.MaxX, true);
                setScale(heatmapGraphPaneByMsLevel[i].YAxis.Scale, bounds.MinY, bounds.MaxY, true);
            }*/
        }

        private void ShowHeatmap(double scanTime, int msLevel)
        {
            if (ionMobilityBinsByMsLevelAndScanTime.Count <= msLevel) throw new ArgumentOutOfRangeException("msLevel", "no ion mobility bins for ms level " + msLevel);

            var ionMobilityBinsByScanTime = ionMobilityBinsByMsLevelAndScanTime[msLevel];
            if (!ionMobilityBinsByScanTime.Contains(scanTime)) throw new ArgumentOutOfRangeException("scanTime", "no ion mobility bins for scan time " + scanTime);


            var heatmapGraphPane = heatmapGraphPaneByMsLevel[msLevel];
            heatmapGraphPane.CurveList.Clear();
            heatmapGraphPane.GraphObjList.Add(new TextObj("Loading...", 0.5, 0.5, CoordType.ChartFraction) {FontSpec = new FontSpec {Border = new Border {IsVisible = false}, IsBold = true, Size = 24}});
            msGraphControl.Refresh();

            var mobilityData = ionMobilityBinsByScanTime[scanTime];
            var heatmapPointsTuple = heatmapPointsCache.GetOrAdd(mobilityData, o => o.Get3dSpectrum(), mobilityData);
            var bounds = heatmapPointsTuple.Item2;

            var g = msGraphControl.CreateGraphics();
            var heatmapData = new HeatMapData(heatmapPointsTuple.Item1);
            heatmapGraphPane.GraphObjList.Clear(); // remove "Loading..."
            heatmapGraphPane.SetPoints(heatmapData, bounds.MinY, bounds.MaxY);

            string isolationMzTitle = String.Empty;
            if (msLevel + 1 == 2)
                isolationMzTitle = " for isolation m/z " + String.Join(", ", isolationMzByScanTime[scanTime].Distinct());

            heatmapGraphPane.Title.Text = String.Format("Ion Mobility Heatmap (ms{0} @ {1:F4} {3}{2})", msLevel + 1, scanTime, isolationMzTitle,
                Properties.Settings.Default.TimeInMinutes ? "min." : "sec.");

            setScale(heatmapGraphPane.XAxis.Scale, bounds.MinX, bounds.MaxX);
            setScale(heatmapGraphPane.YAxis.Scale, bounds.MinY, bounds.MaxY);

            heatmapGraphPane.AxisChange(g);
            heatmapGraphPane.SetScale(g);
            msGraphControl.Refresh();
        }

        private double ToDoubleOrDefault(object value, double defaultValue = 0)
        {
            double result;
            if (Double.TryParse(value.ToString(), out result) == false)
                result = defaultValue;

            return result;
        }

        protected override void OnShown(EventArgs e)
        {
            var dgv = Source.SpectrumListForm.GridView;

            var ionMobilityColumn = dgv.Columns["IonMobility"];
            //if (ionMobilityColumn == null || !ionMobilityColumn.Visible)
            //    throw new InvalidOperationException("cannot show heatmap if SpectrumListForm doesn't have an IonMobility column");

            var scanTimeColumn = dgv.Columns["ScanTime"];
            var ticColumn = dgv.Columns["TotalIonCurrent"];
            var msLevelColumn = dgv.Columns["MsLevel"];
            var isolationMzColumn = dgv.Columns["PrecursorInfo"];
            var dataPointsColumn = dgv.Columns["DataPoints"];
            var indexColumn = dgv.Columns["Index"];
            var idColumn = dgv.Columns["Id"];
            var mainSpectrumList = Source.Source.MSDataFile.run.spectrumList;
            if (scanTimeColumn == null || ticColumn == null || msLevelColumn == null || dataPointsColumn == null)
                throw new InvalidOperationException("scan time, TIC, ms level, and data points columns should never be null");

            // build map of ms levels, to scan times, to scan indices;
            // group keys are ms level and scan time, aggregated values are isolation m/z and spectrum index
            var ionMobilityRows = Source.SpectrumListForm.GetIonMobilityRows().GroupBy(r => new Tuple<int, double>(r.MsLevel, r.ScanTime),
                                                                                       r => new Tuple<double, int>(ToDoubleOrDefault(r.IsolationWindows), r.Index));
            foreach (var group in ionMobilityRows)
            {
                int msLevel = group.Key.Item1 - 1;
                while (heatmapGraphPaneByMsLevel.Count <= msLevel)
                {
                    heatmapGraphPaneByMsLevel.Add(new HeatMapGraphPane()
                    {
                        ShowHeatMap = true,
                        MinDotRadius = 4,
                        MaxDotRadius = 13,
                        //XAxis = {Title = {Text = "Scan Time"}},
                        XAxis = {Title = {Text = "m/z"}},
                        YAxis = {Title = {Text = "Ion Mobility"}},
                        Legend = {IsVisible = false},
                        Title = {Text = String.Format("Ion Mobility Heatmap (ms{0})", msLevel+1), IsVisible = true},
                        LockYAxisAtZero = false
                    });
                    ionMobilityBinsByMsLevelAndScanTime.Add(new Map<double, MobilityData>());
                }

                double scanTime = group.Key.Item2;

                ionMobilityBinsByMsLevelAndScanTime[msLevel][scanTime] = new MobilityData(mainSpectrumList, scanTime,
                                                                                          (int) group.Min(o => o.Item2),
                                                                                          (int) group.Max(o => o.Item2));
                if (msLevel + 1 == 2)
                    isolationMzByScanTime[scanTime] = group.Select(o => o.Item1.ToString()).ToList();

                //double intensity = (double) dgv[ticColumn.Index, i].Value;
            }

            if (ionMobilityBinsByMsLevelAndScanTime.Count == 0)
                throw new InvalidOperationException("no spectrum rows have ion mobility info");

            var g = msGraphControl.CreateGraphics();
            msGraphControl.MasterPane.PaneList.Clear();
            int numColumns = ionMobilityBinsByMsLevelAndScanTime.Count(o => !o.IsNullOrEmpty()); // skip empty MS levels (e.g. files with only MS2)
            var rowCounts = new int[2] {numColumns, numColumns};
            msGraphControl.MasterPane.SetLayout(g, true, rowCounts, new float[2] {0.25f, 0.75f});

            heatmapPointsCache = new MemoryCache<MobilityData, Tuple<List<Point3D>, BoundingBox>>(ionMobilityBinsByMsLevelAndScanTime.Count);

            // first row is control chromatograms
            for (int i = 0; i < heatmapGraphPaneByMsLevel.Count; ++i)
            {
                if (ionMobilityBinsByMsLevelAndScanTime[i].IsNullOrEmpty())
                {
                    ticChromatogramByMsLevel.Add(null);
                    continue; // skip empty MS levels (e.g. files with only MS2)
                }

                var chromatogramPane = new MSGraphPane()
                {
                    Legend = {IsVisible = false},
                    Title = {Text = String.Format("TIC Chromatogram (ms{0})", i + 1), IsVisible = true},
                    Tag = i
                };
                msGraphControl.MasterPane.Add(chromatogramPane);

                ticChromatogramByMsLevel.Add(new ChromatogramControl(Source, i + 1, chromatogramPane));
                msGraphControl.AddGraphItem(chromatogramPane, ticChromatogramByMsLevel[i], true);
            }

            // second row is heatmaps
            for (int i = 0; i < heatmapGraphPaneByMsLevel.Count; ++i)
            {
                if (ionMobilityBinsByMsLevelAndScanTime[i].IsNullOrEmpty())
                    continue; // skip empty MS levels (e.g. files with only MS2)

                var heatmapGraphPane = heatmapGraphPaneByMsLevel[i];
                msGraphControl.MasterPane.Add(heatmapGraphPane);

                heatmapGraphPane.GraphObjList.Add(new TextObj("Click on a chromatogram point to generate an IMS heatmap...", 0.5, 0.5, CoordType.ChartFraction) { FontSpec = new FontSpec { Border = new Border { IsVisible = false }, IsBold = true, Size = 16 } });

                setScale(heatmapGraphPane.XAxis.Scale, 0, 2000);
                setScale(heatmapGraphPane.YAxis.Scale, 0, 100);
                heatmapGraphPane.AxisChange(g);
                heatmapGraphPane.SetScale(g);
            }
            //msGraphControl.PerformAutoScale();
            msGraphControl.MasterPane.DoLayout(g);
            msGraphControl.Refresh();

            base.OnShown(e);
        }

        void foo_Opening( object sender, CancelEventArgs e )
        {
            // close the active form when the tab page strip is right-clicked
            Close();
        }

        bool msGraphControl_MouseMoveEvent( ZedGraphControl sender, MouseEventArgs e )
        {
            MSGraphPane hoverPane = sender.MasterPane.FindPane( e.Location ) as MSGraphPane;
            if( hoverPane == null )
                return false;

            CurveItem nearestCurve;
            int nearestIndex;

            //change the cursor if the mouse is sufficiently close to a point
            if( hoverPane.FindNearestPoint( e.Location, out nearestCurve, out nearestIndex ) )
            {
                msGraphControl.Cursor = Cursors.Cross;
            } else
            {
                msGraphControl.Cursor = Cursors.Default;
            }
            return false;
        }

        private int distance(Point p1, Point p2)
        {
            return (int) Math.Round(Math.Sqrt(Math.Pow((p2.X - p1.X), 2) + Math.Pow((p2.Y - p1.Y), 2)));
        }

        private Point lastMouseDownPos;
        bool msGraphControl_MouseDownEvent(ZedGraphControl sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return false;

            // record mouse down point in control chromatogram (to determine whether the user is doing a drag-to-zoom)
            var focusedPane = sender.MasterPane.FindPane(e.Location) as MSGraphPane;
            if (focusedPane == null || focusedPane.Tag == null)
                return false;

            lastMouseDownPos = e.Location;
            return false;
        }

        bool msGraphControl_MouseUpEvent( ZedGraphControl sender, MouseEventArgs e )
        {
            if (e.Button != MouseButtons.Left)
                return false;

            // change heatmap when user left clicks in a control chromatogram (unless they're dragging a zoom box)

            var focusedPane = sender.MasterPane.FindPane( e.Location ) as MSGraphPane;
            if (focusedPane == null || focusedPane.Tag == null)
                return false;

            CurveItem nearestCurve; int nearestIndex;
            focusedPane.FindNearestPoint( e.Location, out nearestCurve, out nearestIndex );
            if (nearestCurve == null)
                return false;

            if (distance(e.Location, lastMouseDownPos) > 2)
                return false;

            int msLevelIndex = (int) focusedPane.Tag;
            double scanTime = nearestCurve[nearestIndex].X;
            ticChromatogramByMsLevel[msLevelIndex].SelectTime(scanTime);
            ShowHeatmap(scanTime, msLevelIndex);
            return false;
        }

        private void GraphForm_ResizeBegin( object sender, EventArgs e )
        {
            SuspendLayout();
            msGraphControl.Visible = false;
            Refresh();
        }

        private void GraphForm_ResizeEnd( object sender, EventArgs e )
        {
            ResumeLayout();
            msGraphControl.Visible = true;
            Refresh();
        }
    }
}