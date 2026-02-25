//
// $Id$
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
using pwiz.Common.Collections;
using SpyTools;

namespace seems
{
    public partial class TimeMzHeatmapForm : ManagedDockableForm
    {
        public pwiz.MSGraph.MSGraphControl ZedGraphControl { get { return msGraphControl; } }

        private class BoundingBox
        {
            public double MinX { get; set; }
            public double MinY { get; set; }
            public double MaxX { get; set; }
            public double MaxY { get; set; }
        }

        private struct MobilityBin
        {
            public MobilityBin(double ionMobility, int binIndex) : this()
            {
                IonMobility = ionMobility;
                BinIndex = binIndex;
            }

            public double IonMobility { get; private set; }
            public int BinIndex { get; private set; }
        }

        private List<BoundingBox> heatmapBoundsByMsLevel; // updated when scan time changes
        private List<HeatMapGraphPane> heatmapGraphPaneByMsLevel; // 1 heatmap per ms level

        public TimeMzHeatmapForm(Manager manager, ManagedDataSource source)
        {
            InitializeComponent();

            Manager = manager;
            Source = source;
            heatmapGraphPaneByMsLevel = new List<HeatMapGraphPane>();
            heatmapBoundsByMsLevel = new List<BoundingBox>();

            msGraphControl.BorderStyle = BorderStyle.None;

            msGraphControl.MasterPane.InnerPaneGap = 1;
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

            Text = TabText = source.Source.Name + " Heatmaps";

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
            for (int i = 0; i < heatmapGraphPaneByMsLevel.Count; ++i)
            {
                var bounds = heatmapBoundsByMsLevel[i];
                setScale(heatmapGraphPaneByMsLevel[i].XAxis.Scale, bounds.MinX, bounds.MaxX, true);
                setScale(heatmapGraphPaneByMsLevel[i].YAxis.Scale, bounds.MinY, bounds.MaxY, true);
            }
        }

        private void ShowHeatmaps()
        {

            var dgv = Source.SpectrumListForm.GridView;
            var scanTimeColumn = dgv.Columns["ScanTime"];
            var msLevelColumn = dgv.Columns["MsLevel"];
            var heatmapPointsByMsLevel = new List<List<Point3D>>(dgv.RowCount);

            var msLevels = new Set<int>();
            for (int i = 0; i < dgv.RowCount; ++i)
                msLevels.Add((int)dgv[msLevelColumn.Index, i].Value - 1);

            while (heatmapPointsByMsLevel.Count <= msLevels.Max)
                heatmapPointsByMsLevel.Add(new List<Point3D>());

            var peakPicker = new pwiz.CLI.analysis.SpectrumList_PeakPicker(Source.Source.MSDataFile.run.spectrumList, new pwiz.CLI.analysis.CwtPeakDetector(1, 0.5), true, new int[]{1});
            for (int i = 0; i < dgv.RowCount; ++i)
            {
                int msLevel = (int)dgv[msLevelColumn.Index, i].Value - 1;
                var heatmapGraphPane = heatmapGraphPaneByMsLevel[msLevel];
                var heatmapPoints = heatmapPointsByMsLevel[msLevel];

                if (i == 0 || i+1 == dgv.RowCount || ((i+1) % 1000) == 0)
                {
                    heatmapGraphPane.CurveList.Clear();
                    heatmapGraphPane.GraphObjList.Add(new TextObj(String.Format("Loading {0}/{1}", i+1, dgv.RowCount), 0.5, 0.5, CoordType.ChartFraction) { FontSpec = new FontSpec { Border = new Border { IsVisible = false }, IsBold = true, Size = 24 } });
                    msGraphControl.Refresh();
                }
                Application.DoEvents();

                var bounds = heatmapBoundsByMsLevel[msLevel];
                var spectrum = Source.GetMassSpectrum(Source.Source.Spectra[i].Index, peakPicker);
                double scanTime = (double)dgv[scanTimeColumn.Index, i].Value;
                var points = spectrum.Points;
                for (int j = 0; j < points.Count; ++j)
                {
                    double intensity = points[j].Y;
                    if (intensity == 0)
                        continue;

                    double mz = points[j].X;
                    bounds.MinX = Math.Min(bounds.MinX, mz);
                    bounds.MinY = Math.Min(bounds.MinY, scanTime);
                    bounds.MaxX = Math.Max(bounds.MaxX, mz);
                    bounds.MaxY = Math.Max(bounds.MaxY, scanTime);

                    heatmapPoints.Add(new Point3D(mz, scanTime, intensity));
                }
            }

            var g = msGraphControl.CreateGraphics();
            foreach (int msLevel in msLevels)
            {
                var bounds = heatmapBoundsByMsLevel[msLevel];
                var heatmapData = new HeatMapData(heatmapPointsByMsLevel[msLevel]);
                var heatmapGraphPane = heatmapGraphPaneByMsLevel[msLevel];
                heatmapGraphPane.GraphObjList.Clear(); // remove "Loading..."
                heatmapGraphPane.SetPoints(heatmapData, bounds.MinY, bounds.MaxY);
                heatmapGraphPane.Title.Text = String.Format("Time to m/z Heatmap (ms{0})", msLevel + 1);

                setScale(heatmapGraphPane.XAxis.Scale, bounds.MinX, bounds.MaxX);
                setScale(heatmapGraphPane.YAxis.Scale, bounds.MinY, bounds.MaxY);

                heatmapGraphPane.AxisChange(g);
                heatmapGraphPane.SetScale(g);
                msGraphControl.Refresh();
            }
        }

        protected override void OnShown(EventArgs e)
        {
            var dgv = Source.SpectrumListForm.GridView;
            var scanTimeColumn = dgv.Columns["ScanTime"];
            var ticColumn = dgv.Columns["TotalIonCurrent"];
            var msLevelColumn = dgv.Columns["MsLevel"];
            var dataPointsColumn = dgv.Columns["DataPoints"];
            if (scanTimeColumn == null || ticColumn == null || msLevelColumn == null || dataPointsColumn == null)
                throw new InvalidOperationException("scan time, TIC, ms level, and data points columns should never be null");

            // build map of ms levels, to scan times, to scan indices

            for (int i = 0; i < dgv.RowCount; ++i)
            {
                int msLevel = (int) dgv[msLevelColumn.Index, i].Value - 1;
                while (heatmapGraphPaneByMsLevel.Count <= msLevel)
                {
                    heatmapBoundsByMsLevel.Add(new BoundingBox { MinX = Double.MaxValue, MaxX = Double.MinValue, MinY = Double.MaxValue, MaxY = Double.MinValue });
                    heatmapGraphPaneByMsLevel.Add(new HeatMapGraphPane()
                    {
                        ShowHeatMap = true,
                        MinDotRadius = 4,
                        MaxDotRadius = 13,
                        //XAxis = {Title = {Text = "Scan Time"}},
                        XAxis = {Title = {Text = "m/z"}},
                        YAxis = {Title = {Text = "Scan Time"}},
                        Legend = {IsVisible = false},
                        Title = { Text = String.Format("Time to m/z Heatmap (ms{0})", msLevel + 1), IsVisible = true },
                        LockYAxisAtZero = false
                    });
                }
            }

            var g = msGraphControl.CreateGraphics();
            msGraphControl.MasterPane.PaneList.Clear();
            int numColumns = heatmapGraphPaneByMsLevel.Count;
            var rowCounts = new int[1] {numColumns};
            msGraphControl.MasterPane.SetLayout(g, true, rowCounts, new float[1] {1.0f});

            // second row is heatmaps
            for (int i = 0; i < heatmapGraphPaneByMsLevel.Count; ++i)
            {
                var heatmapGraphPane = heatmapGraphPaneByMsLevel[i];
                msGraphControl.MasterPane.Add(heatmapGraphPane);

                var bounds = heatmapBoundsByMsLevel[i];
                setScale(heatmapGraphPane.XAxis.Scale, 0, 2000);
                setScale(heatmapGraphPane.YAxis.Scale, 0, 100);
                heatmapGraphPane.AxisChange(g);
                heatmapGraphPane.SetScale(g);
            }
            //msGraphControl.PerformAutoScale();
            msGraphControl.MasterPane.DoLayout(g);
            msGraphControl.Refresh();

            ShowHeatmaps();

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