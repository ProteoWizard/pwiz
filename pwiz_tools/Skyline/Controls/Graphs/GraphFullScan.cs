/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Windows.Forms;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.CommonMsData;
using pwiz.MSGraph;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using ZedGraph;
using Thread = System.Threading.Thread;
using Transition = pwiz.Skyline.Model.Transition;
using PeakType = pwiz.Skyline.Model.Results.MsDataFileScanHelper.PeakType;

namespace pwiz.Skyline.Controls.Graphs
{
    public partial class GraphFullScan : DockableFormEx, IGraphContainer, IMzScalePlot, IMenuControlImplementer
    {
        private const int MIN_DOT_RADIUS = 4;
        private const int MAX_DOT_RADIUS = 13;
        private const string TAG_IM_FILTER_BAND = "IMFilterBand";

        private readonly IDocumentUIContainer _documentContainer;
        private readonly GraphHelper _graphHelper;
        private HeatMapData _heatMapData;
        private HeatMapGraphPane _heatMapPane;
        private MSGraphPane _stickSpectrumPane;
        private MSGraphPane _mobilogramPane;
        private MSGraphPane _emptySpacerPane;
        // Splitter geometry helpers (4-pane layout only)
        private const float SPLITTER_HIT_PX = 4f; // cursor proximity threshold
        private const float MIN_COL_FRACTION = 0.30f;
        private const float MAX_COL_FRACTION = 0.95f;
        private const float MIN_ROW_FRACTION = 0.15f;
        private const float MAX_ROW_FRACTION = 0.85f;
        // User-draggable fractions; persisted via Settings.Default
        private float ColumnFraction
        {
            get => Math.Min(MAX_COL_FRACTION, Math.Max(MIN_COL_FRACTION, Settings.Default.FullScanMobilogramColumnFraction));
            set => Settings.Default.FullScanMobilogramColumnFraction = Math.Min(MAX_COL_FRACTION, Math.Max(MIN_COL_FRACTION, value));
        }
        private float RowFraction
        {
            get => Math.Min(MAX_ROW_FRACTION, Math.Max(MIN_ROW_FRACTION, Settings.Default.FullScanStickRowFraction));
            set => Settings.Default.FullScanStickRowFraction = Math.Min(MAX_ROW_FRACTION, Math.Max(MIN_ROW_FRACTION, value));
        }
        private enum SplitterDrag { None, Vertical, Horizontal, Both }
        private SplitterDrag _activeDrag = SplitterDrag.None;
        private bool IsDualPane => _stickSpectrumPane != null;
        private bool IsMobilogramPaneVisible => _mobilogramPane != null;
        private double _maxMz;
        private double _maxIntensity;
        private double _minIonMobility;
        private double _maxIonMobility;
        private bool _zoomXAxis;
        private bool _zoomYAxis;
        private readonly MsDataFileScanHelper _msDataFileScanHelper;
        private LibraryRankedSpectrumInfo _rmis;

        // status info to calculate point dot products
        private SpectrumPeaksInfo.MI[] _peaks;
        private GraphSpectrum.Precursor _precursor;

        private int[] _transitionIndex;
        private MzRange _requestedRange;

        private bool _showIonSeriesAnnotations;

        // Tooltip shows realtime info about what's under the cursor
        // as it moves, goes away after a few seconds of no movement
        private readonly CursorTrackingTip _cursorTip;

        private MSGraphControl graphControl => graphControlExtension.Graph;

        public GraphFullScan(IDocumentUIContainer documentUIContainer)
        {
            InitializeComponent();

            _heatMapPane = new FullScanHeatMapGraphPane
            {
                MinDotRadius = MIN_DOT_RADIUS,
                MaxDotRadius = MAX_DOT_RADIUS,
                ShowHeatMap = !Settings.Default.SumScansFullScan
            };
            graphControl.GraphPane = _heatMapPane;
            graphControl.GraphPane.AllowLabelOverlap = true;
            graphControl.ContextMenuBuilder += graphControl_ContextMenuBuilder;
            graphControl.MouseMoveEvent += graphControl_MouseMove;
            graphControl.MouseClick += graphControl_MouseClick;
            graphControl.ZoomEvent += graphControl_ZoomEvent;
            graphControl.Resize += graphControl_Resize;
            // Re-pin mobilogram chart rect on every paint using whatever chart rect
            // heatmap actually ended up with. Initial layout paths can produce a stale
            // heatmap rect at pin-time; catching it in Paint avoids that race.
            graphControl.Paint += (sender, e) =>
            {
                if (IsMobilogramPaneVisible)
                    RepinMobilogramIfDrifted();
                else if (IsDualPane)
                {
                    var mpR = graphControl.MasterPane.Rect;
                    if (mpR.Height > 0 && Math.Abs(_stickSpectrumPane.Rect.Height - mpR.Height * RowFraction) > 1f)
                        AdjustTwoPaneRowHeights();
                }
            };
            // Use ZedGraph's intercepting events so we can suppress its default pan/zoom
            // when the user is dragging the splitter.
            graphControl.MouseDownEvent += graphControl_SplitterMouseDown;
            graphControl.MouseMove += graphControl_SplitterMouseMove;
            graphControl.MouseUpEvent += graphControl_SplitterMouseUp;

            Icon = Resources.SkylineData;
            _graphHelper = GraphHelper.Attach(graphControl);
            _documentContainer = documentUIContainer;
            _documentContainer.ListenUI(OnDocumentUIChanged);

            _msDataFileScanHelper =
                new MsDataFileScanHelper(SetSpectra, HandleLoadScanException,
                    false); // We need zero intensity points for proper display

            GraphPane.Title.IsVisible = true;
            GraphPane.Legend.IsVisible = false;
            // Make sure to use italics for "m/z"
            AbstractMSGraphItem.SetAxisText(GraphPane.XAxis, GraphsResources.AbstractMSGraphItem_CustomizeXAxis_MZ);

            magnifyBtn.Checked = Settings.Default.AutoZoomFullScanGraph;
            spectrumBtn.Checked = Settings.Default.SumScansFullScan;
            filterBtn.Checked = Settings.Default.FilterIonMobilityFullScan;
            toolStripButtonShowAnnotations.Checked = Settings.Default.ShowFullScanAnnotations;
            _showIonSeriesAnnotations = Settings.Default.ShowFullScanAnnotations;

            mobilogramBtn.Checked = Settings.Default.ShowMobilogramFullScan;

            magnifyBtn.CheckedChanged += magnifyBtn_CheckedChanged;
            spectrumBtn.CheckedChanged += spectrumBtn_CheckedChanged;
            filterBtn.CheckedChanged += filterBtn_CheckedChanged;
            mobilogramBtn.CheckedChanged += mobilogramBtn_CheckedChanged;
            toolStripButtonShowAnnotations.CheckedChanged += toolStripButtonShowAnnotations_CheckedChanged;


            spectrumBtn.Visible = false;
            filterBtn.Visible = false;
            mobilogramBtn.Visible = false;
            lblScanId.Visible = false; // you might want to show the scan index for debugging
            comboBoxPeakType.Items.Clear();
            comboBoxPeakType.Items.Add(_msDataFileScanHelper.GetPeakTypeLocalizedName(PeakType.chromDefault));
            comboBoxPeakType.Items.Add(_msDataFileScanHelper.GetPeakTypeLocalizedName(PeakType.centroided));
            comboBoxPeakType.Items.Add(_msDataFileScanHelper.GetPeakTypeLocalizedName(PeakType.profile));
            var peakType = _msDataFileScanHelper.ParsePeakTypeEnumName(Settings.Default.FullScanPeakType);
            comboBoxPeakType.SelectedItem = _msDataFileScanHelper.GetPeakTypeLocalizedName(peakType);
            this.comboBoxPeakType.SelectedIndexChanged += this.comboBoxPeakType_SelectedIndexChanged;
            graphControlExtension.RestorePropertiesSheet();

            // Tooltip shows realtime info about what's under the cursor
            _cursorTip = new CursorTrackingTip(graphControl, pt =>
                _msDataFileScanHelper.MsDataSpectra != null ? GetTooltipTable(new PointF(pt.X, pt.Y)) : null);
        }

        public ZedGraphControl ZedGraphControl
        {
            get { return graphControl; }
        }

        /// <summary>
        /// Configure dual-pane layout: stick spectrum on top, heatmap on bottom.
        /// When <paramref name="includeMobilogram"/> is true, adds a 4-pane 2x2 grid
        /// (stick + empty spacer on row 0, heatmap + mobilogram on row 1).
        /// </summary>
        private void SetupDualPaneLayout(bool includeMobilogram)
        {
            if (_stickSpectrumPane == null)
            {
                _stickSpectrumPane = new FullScanStickSpectrumPane(new LabelBoundsCache())
                {
                    Title = { IsVisible = true },
                    Legend = { IsVisible = false },
                    AllowLabelOverlap = true,
                    Border = { IsVisible = false }
                };
                AbstractMSGraphItem.SetAxisText(_stickSpectrumPane.XAxis,
                    GraphsResources.AbstractMSGraphItem_CustomizeXAxis_MZ);
                _stickSpectrumPane.YAxis.Title.Text =
                    GraphsResources.AbstractMSGraphItem_CustomizeYAxis_Intensity;
                _stickSpectrumPane.XAxis.Title.IsVisible = false;
            }

            // Force both Y-axes to reserve the same width so chart areas align horizontally
            const float yAxisMinSpace = 80f;
            _stickSpectrumPane.YAxis.MinSpace = yAxisMinSpace;
            _heatMapPane.YAxis.MinSpace = yAxisMinSpace;

            if (includeMobilogram)
            {
                if (_mobilogramPane == null)
                    _mobilogramPane = CreateMobilogramPane(_heatMapPane);
                if (_emptySpacerPane == null)
                    _emptySpacerPane = CreateEmptySpacerPane();
            }
            else
            {
                _mobilogramPane = null;
                _emptySpacerPane = null;
            }

            var mp = graphControl.MasterPane;

            bool alreadyConfigured;
            if (includeMobilogram)
            {
                alreadyConfigured = mp.PaneList.Count == 4 &&
                                    ReferenceEquals(mp.PaneList[0], _stickSpectrumPane) &&
                                    ReferenceEquals(mp.PaneList[1], _emptySpacerPane) &&
                                    ReferenceEquals(mp.PaneList[2], _heatMapPane) &&
                                    ReferenceEquals(mp.PaneList[3], _mobilogramPane);
            }
            else
            {
                alreadyConfigured = mp.PaneList.Count == 2 &&
                                    ReferenceEquals(mp.PaneList[0], _stickSpectrumPane) &&
                                    ReferenceEquals(mp.PaneList[1], _heatMapPane);
            }

            if (!alreadyConfigured)
            {
                mp.PaneList.Clear();
                mp.InnerPaneGap = 0;
                // Tighten gap between panes while keeping enough room for Y-axis labels
                _stickSpectrumPane.Margin.Bottom = 0;
                _heatMapPane.Margin.Top = 10;
                using (var g = graphControl.CreateGraphics())
                {
                    float topFrac = RowFraction;
                    if (includeMobilogram)
                    {
                        // 2 rows × 2 cols: stick+spacer on top, heatmap+mobilogram below
                        mp.SetLayout(g, true, new[] { 2, 2 }, new[] { topFrac, 1 - topFrac });
                        mp.Add(_stickSpectrumPane);
                        mp.Add(_emptySpacerPane);
                        mp.Add(_heatMapPane);
                        mp.Add(_mobilogramPane);
                    }
                    else
                    {
                        mp.SetLayout(g, true, new[] { 1, 1 }, new[] { topFrac, 1 - topFrac });
                        mp.Add(_stickSpectrumPane);
                        mp.Add(_heatMapPane);
                    }
                    mp.DoLayout(g);
                    if (includeMobilogram)
                        AdjustFourPaneColumnWidths();
                    else
                        AdjustTwoPaneRowHeights();
                }
            }
            // Don't use ZedGraph's global X-axis sync — it would force the mobilogram's
            // intensity X-axis to track the heatmap's m/z range. We sync stick↔heatmap X
            // manually in the zoom handler instead.
            graphControl.IsSynchronizeXAxes = false;
        }

        /// <summary>
        /// After MasterPane.DoLayout (which assigns equal column widths), override the
        /// per-pane Rects so the right column (spacer + mobilogram) occupies only
        /// MOBILOGRAM_COLUMN_FRACTION of the total width.
        /// </summary>
        private void AdjustFourPaneColumnWidths()
        {
            if (_stickSpectrumPane == null || _emptySpacerPane == null ||
                _heatMapPane == null || _mobilogramPane == null)
                return;

            // Use MasterPane.Rect as the authoritative source so we don't fold compressed
            // pane Rects from a previous bad layout back into the math (would shrink each call).
            var mpRect = graphControl.MasterPane.Rect;
            float x0 = mpRect.X;
            float y0 = mpRect.Y;
            float totalW = mpRect.Width;
            float totalH = mpRect.Height;
            if (totalW <= 0 || totalH <= 0)
                return;

            float leftFrac = ColumnFraction;
            float topFrac = RowFraction;
            float leftW = totalW * leftFrac;
            float rightW = totalW - leftW;
            float topH = totalH * topFrac;
            float botH = totalH - topH;
            float xMid = x0 + leftW;
            float yMid = y0 + topH;

            _stickSpectrumPane.Rect = new RectangleF(x0, y0, leftW, topH);
            _emptySpacerPane.Rect = new RectangleF(xMid, y0, rightW, topH);
            _heatMapPane.Rect = new RectangleF(x0, yMid, leftW, botH);
            _mobilogramPane.Rect = new RectangleF(xMid, yMid, rightW, botH);

            AlignMobilogramChartToHeatmap();
        }

        /// <summary>
        /// Apply RowFraction to the 2-pane (no-mobilogram) layout so the horizontal
        /// splitter still drives row heights when the mobilogram is hidden.
        /// </summary>
        private void AdjustTwoPaneRowHeights()
        {
            if (_stickSpectrumPane == null || _heatMapPane == null) return;
            var mpRect = graphControl.MasterPane.Rect;
            if (mpRect.Width <= 0 || mpRect.Height <= 0) return;
            float topFrac = RowFraction;
            float topH = mpRect.Height * topFrac;
            float botH = mpRect.Height - topH;
            _stickSpectrumPane.Rect = new RectangleF(mpRect.X, mpRect.Y, mpRect.Width, topH);
            _heatMapPane.Rect = new RectangleF(mpRect.X, mpRect.Y + topH, mpRect.Width, botH);
        }

        /// <summary>
        /// Pin the mobilogram pane's Chart.Rect so it aligns vertically (and in height) with
        /// the heatmap's Chart.Rect. Without this, the heatmap's legend/margins cause the
        /// two chart areas to have different tops/bottoms, so IM values don't line up.
        /// </summary>
        /// <summary>
        /// If heatmap's actual chart rect no longer matches what we pinned, re-pin and
        /// invalidate so the next paint catches up. Called from the Paint handler.
        /// </summary>
        private void RepinMobilogramIfDrifted()
        {
            if (!IsMobilogramPaneVisible) return;
            // Only re-apply the 2x2 split if pane Rects don't match expected — calling
            // every paint causes a repaint loop because Rect assignment invalidates.
            var mpRect = graphControl.MasterPane.Rect;
            if (mpRect.Width > 0 && mpRect.Height > 0)
            {
                float expectedLeftW = mpRect.Width * ColumnFraction;
                float expectedTopH = mpRect.Height * RowFraction;
                if (Math.Abs(_heatMapPane.Rect.Width - expectedLeftW) > 1f ||
                    Math.Abs(_stickSpectrumPane.Rect.Height - expectedTopH) > 1f)
                {
                    AdjustFourPaneColumnWidths();
                }
            }
            var heat = _heatMapPane.Chart.Rect;
            if (heat.Height <= 0) return;

            // Always resync Y-axis state from heatmap — scale limits, steps, and auto
            // flags — so mobilogram Y values map to the exact same pixels as heatmap Y.
            var heatY = _heatMapPane.YAxis.Scale;
            var mobY = _mobilogramPane.YAxis.Scale;
            bool yDrifted = mobY.Min != heatY.Min || mobY.Max != heatY.Max ||
                            mobY.MajorStep != heatY.MajorStep || mobY.MinorStep != heatY.MinorStep;
            if (yDrifted)
                SyncMobilogramYAxisFromHeatmap();

            var mob = _mobilogramPane.Chart.Rect;
            bool rectDrifted = Math.Abs(mob.Y - heat.Y) >= 0.5f ||
                               Math.Abs(mob.Height - heat.Height) >= 0.5f;
            if (rectDrifted)
            {
                var paneRect = _mobilogramPane.Rect;
                float chartX = paneRect.X + _mobilogramPane.YAxis.MinSpace;
                float chartRight = paneRect.Right - 10;
                float chartW = Math.Max(1, chartRight - chartX);
                _mobilogramPane.Chart.Rect = new RectangleF(chartX, heat.Y, chartW, heat.Height);
            }

            if (yDrifted || rectDrifted)
                graphControl.Invalidate();
        }

        // ---- Cruciform splitter for 2x2 pane resize ----

        private SplitterDrag HitTestSplitter(Point pt)
        {
            if (!IsDualPane) return SplitterDrag.None;
            float yMid = _heatMapPane.Rect.Y;
            bool nearH = Math.Abs(pt.Y - yMid) <= SPLITTER_HIT_PX;
            if (IsMobilogramPaneVisible)
            {
                float xMid = _emptySpacerPane.Rect.X;
                bool nearV = Math.Abs(pt.X - xMid) <= SPLITTER_HIT_PX;
                if (nearV && nearH) return SplitterDrag.Both;
                if (nearV) return SplitterDrag.Vertical;
            }
            if (nearH) return SplitterDrag.Horizontal;
            return SplitterDrag.None;
        }

        private void graphControl_SplitterMouseMove(object sender, MouseEventArgs e)
        {
            if (_activeDrag != SplitterDrag.None)
            {
                ApplyDrag(e.Location);
                return;
            }
            // Stick pane should always be X-only on wheel zoom — disable vertical zoom
            // dynamically when the cursor is over it. (ZedGraph's wheel handler reads
            // IsEnableVZoom at the moment the wheel event fires.)
            if (IsDualPane)
            {
                var underCursor = graphControl.MasterPane.FindPane(e.Location);
                bool vZoomOk = !ReferenceEquals(underCursor, _stickSpectrumPane);
                if (graphControl.IsEnableVZoom != vZoomOk)
                    graphControl.IsEnableVZoom = vZoomOk;
            }
            if (!IsDualPane) return;
            var hit = HitTestSplitter(e.Location);
            switch (hit)
            {
                case SplitterDrag.Vertical:
                    graphControl.Cursor = Cursors.VSplit; break;
                case SplitterDrag.Horizontal:
                    graphControl.Cursor = Cursors.HSplit; break;
                case SplitterDrag.Both:
                    graphControl.Cursor = Cursors.SizeAll; break;
            }
        }

        private bool graphControl_SplitterMouseDown(ZedGraphControl sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return false;
            var hit = HitTestSplitter(e.Location);
            if (hit == SplitterDrag.None) return false;
            _activeDrag = hit;
            graphControl.Capture = true;
            return true; // suppress ZedGraph pan/zoom while dragging splitter
        }

        private bool graphControl_SplitterMouseUp(ZedGraphControl sender, MouseEventArgs e)
        {
            if (_activeDrag == SplitterDrag.None) return false;
            _activeDrag = SplitterDrag.None;
            graphControl.Capture = false;
            Settings.Default.Save();
            return true;
        }

        private void ApplyDrag(Point pt)
        {
            var mpRect = graphControl.MasterPane.Rect;
            if (mpRect.Width <= 0 || mpRect.Height <= 0) return;

            if (_activeDrag == SplitterDrag.Vertical || _activeDrag == SplitterDrag.Both)
                ColumnFraction = (pt.X - mpRect.X) / mpRect.Width;
            if (_activeDrag == SplitterDrag.Horizontal || _activeDrag == SplitterDrag.Both)
                RowFraction = (pt.Y - mpRect.Y) / mpRect.Height;

            if (IsMobilogramPaneVisible)
                AdjustFourPaneColumnWidths();
            else
                AdjustTwoPaneRowHeights();
            graphControl.Invalidate();
        }

        private void AlignMobilogramChartToHeatmap()
        {
            if (!IsMobilogramPaneVisible)
                return;
            // Compute the heatmap's chart rect live (CalcChartRect returns the rect but
            // only assigns it to Chart.Rect inside GraphPane.Draw when IsRectAuto=true).
            RectangleF heat;
            using (var g = graphControl.CreateGraphics())
            {
                // AxisChange populates scale data so CalcChartRect returns what Draw will use
                _heatMapPane.AxisChange(g);
                heat = _heatMapPane.CalcChartRect(g);
            }
            if (heat.Height <= 0)
                return;
            var paneRect = _mobilogramPane.Rect;
            // Offset chartX by Y-axis space so labels have room to render on the left
            float chartX = paneRect.X + _mobilogramPane.YAxis.MinSpace;
            float chartRight = paneRect.Right - 10;
            float chartW = Math.Max(1, chartRight - chartX);
            _mobilogramPane.Chart.Rect = new RectangleF(chartX, heat.Y, chartW, heat.Height);
            // Setting Rect flips IsRectAuto to false automatically
        }

        private MSGraphPane CreateMobilogramPane(HeatMapGraphPane heatMap)
        {
            var pane = new MSGraphPane
            {
                Title = { IsVisible = false },
                Legend = { IsVisible = false },
                Border = { IsVisible = false },
                AllowLabelOverlap = true,
            };
            // No top/right border — only the XAxis (bottom) and YAxis (left) lines show
            pane.Chart.Border.IsVisible = false;
            pane.XAxis.Title.IsVisible = true;
            pane.XAxis.Title.Text = GraphsResources.AbstractMSGraphItem_CustomizeYAxis_Intensity;
            // MSGraphPane defaults this to true, which would snap Y.Min to 0 on AxisChange.
            pane.LockYAxisMinAtZero = false;
            CopyYAxisFromHeatmap(pane, heatMap);
            // Hide Y-axis numbers (heatmap shows them — avoid redundancy) but keep tick marks
            // on both sides of the axis line to match heatmap's visual style.
            pane.YAxis.Title.IsVisible = false;
            pane.YAxis.Scale.IsVisible = false;
            pane.YAxis.MajorTic.IsOutside = true;
            pane.YAxis.MajorTic.IsInside = true;
            pane.YAxis.MinorTic.IsOutside = true;
            pane.YAxis.MinorTic.IsInside = true;
            pane.YAxis.MinSpace = 4; // small, just enough for tick marks
            // Match heatmap pane's margins so Chart.Rect Y/Height auto-align
            pane.Margin.Left = 0;
            pane.Margin.Right = ZedGraph.Margin.Default.Right;
            pane.Margin.Top = 10;
            pane.Margin.Bottom = ZedGraph.Margin.Default.Bottom;
            pane.XAxis.Scale.MinAuto = false;
            pane.XAxis.Scale.MaxAuto = false;
            return pane;
        }

        /// <summary>
        /// Copy all Y-axis state from the heatmap onto the mobilogram pane. Called at
        /// create time and every time heatmap Y changes, so mobilogram rows line up with
        /// heatmap rows pixel-for-pixel.
        /// </summary>
        private static void CopyYAxisFromHeatmap(MSGraphPane target, HeatMapGraphPane heatMap)
        {
            var src = heatMap.YAxis.Scale;
            var dst = target.YAxis.Scale;
            dst.Min = src.Min;
            dst.Max = src.Max;
            dst.MinAuto = false;
            dst.MaxAuto = false;
            // Pin steps (not Auto) so mobilogram's AxisChange won't recompute them
            // from its own data and produce denser ticks than heatmap's.
            dst.MajorStep = src.MajorStep;
            dst.MinorStep = src.MinorStep;
            dst.MajorStepAuto = false;
            dst.MinorStepAuto = false;
        }

        private MSGraphPane CreateEmptySpacerPane()
        {
            var pane = new MSGraphPane
            {
                Title = { IsVisible = false },
                Legend = { IsVisible = false },
                Border = { IsVisible = false },
            };
            pane.Chart.Border.IsVisible = false;
            pane.XAxis.IsVisible = false;
            pane.YAxis.IsVisible = false;
            pane.X2Axis.IsVisible = false;
            pane.Y2Axis.IsVisible = false;
            pane.Margin.All = 0;
            return pane;
        }

        /// <summary>
        /// Restore single-pane layout (heatmap only or stick only).
        /// </summary>
        private void SetupSinglePaneLayout()
        {
            _stickSpectrumPane = null;
            _mobilogramPane = null;
            _emptySpacerPane = null;
            graphControl.IsSynchronizeXAxes = false;
            var mp = graphControl.MasterPane;
            if (mp.PaneList.Count != 1 || !ReferenceEquals(mp.PaneList[0], _heatMapPane))
            {
                mp.PaneList.Clear();
                mp.Add(_heatMapPane);
            }
            // Reset margins and force layout recalculation
            _heatMapPane.Margin.Top = ZedGraph.Margin.Default.Top;
            _heatMapPane.Margin.Right = ZedGraph.Margin.Default.Right;
            using (var g = graphControl.CreateGraphics())
            {
                mp.SetLayout(g, PaneLayout.SingleColumn);
                mp.DoLayout(g);
            }
        }

        private void SetSpectra(MsDataSpectrum[] spectra)
        {
            BeginInvoke(new Action(() => SetSpectraUI(spectra)));
        }

        private void SetSpectraUI(MsDataSpectrum[] spectra)
        {
            _msDataFileScanHelper.MsDataSpectra = spectra;
            if (_msDataFileScanHelper.MsDataSpectra == null || !_msDataFileScanHelper.MsDataSpectra.Any())
                return;
            _rmis = null;


            var peakType = _msDataFileScanHelper.ParsePeakTypeEnumName(Settings.Default.FullScanPeakType);
            if (peakType != PeakType.chromDefault)
            {
                var requestedCentroids = (peakType == PeakType.centroided);
                if (spectra[0].Centroided != requestedCentroids)
                {
                    MessageDlg.Show(this, string.Format(
                        GraphsResources.GraphFullScan_SetSpectraUI__peak_type_not_available,
                        _msDataFileScanHelper.GetPeakTypeLocalizedName(requestedCentroids
                            ? PeakType.centroided
                            : PeakType.profile),
                        _msDataFileScanHelper.GetPeakTypeLocalizedName(spectra[0].Centroided
                            ? PeakType.centroided
                            : PeakType.profile)));

                    this.comboBoxPeakType.SelectedIndexChanged -= this.comboBoxPeakType_SelectedIndexChanged;
                    Settings.Default.FullScanPeakType = PeakType.chromDefault.ToString();
                    comboBoxPeakType.SelectedItem = _msDataFileScanHelper.GetPeakTypeLocalizedName(PeakType.chromDefault);
                    this.comboBoxPeakType.SelectedIndexChanged += this.comboBoxPeakType_SelectedIndexChanged;
                }
            }

            _heatMapData = null;
            // Find max values.
            _maxMz = 0;
            _maxIntensity = 0;
            GetMaxMzIntensity(out _maxMz, out _maxIntensity);
            GetIonMobilityRange(out _minIonMobility, out _maxIonMobility);
            
            _requestedRange = new MzRange(0, _maxMz * 1.1);
            if (Settings.Default.SyncMZScale)
            {
                if(_documentContainer is ISpectrumScaleProvider scaleProvider)
                    _requestedRange = scaleProvider.GetMzRange(SpectrumControlType.LibraryMatch) ?? _requestedRange;
            }

            if (_zoomXAxis)
            {
                _zoomXAxis = false;
                ZoomXAxis();
            }

            if (_zoomYAxis)
            {
                _zoomYAxis = false;
                ZoomYAxis();
            }

            UpdateUI();
            FireZoomEvent();
        }

        private void HandleLoadScanException(Exception ex)
        {
            BeginInvoke(new Action(() => HandleLoadScanExceptionUI(ex)));
        }

        private void HandleLoadScanExceptionUI(Exception ex)
        {
            GraphPane.Title.Text = GraphsResources.GraphFullScan_LoadScan_Spectrum_unavailable;
            MessageDlg.ShowException(this, ex);
        }

        private SrmDocument DocumentUI
        {
            get { return _documentContainer.DocumentUI; }
        }

        private MSGraphPane GraphPane
        {
            get { return _heatMapPane; }
        }

        private Color GetTransitionColor(TransitionFullScanInfo t)
        {
            if (_showIonSeriesAnnotations)
                return IonTypeExtension.GetTypeColor((t.Id as Transition)?.IonType);
            else
                return t.Color;
        }
        public void ShowSpectrum(IScanProvider scanProvider, int transitionIndex, int scanIndex, int? optStep)
        {
            _msDataFileScanHelper.UpdateScanProvider(scanProvider, transitionIndex, scanIndex, optStep);
            if (scanProvider != null)
            {

                comboBoxScanType.Items.Clear();
                foreach (var source in new[] { ChromSource.ms1, ChromSource.fragment, ChromSource.sim })
                {
                    foreach (var transition in _msDataFileScanHelper.ScanProvider.Transitions)
                    {
                        if (transition.Source == source)
                        {
                            comboBoxScanType.Items.Add(_msDataFileScanHelper.NameFromSource(source));
                            break;
                        }
                    }
                }
                comboBoxScanType.SelectedIndexChanged -= comboBoxScanType_SelectedIndexChanged;
                comboBoxScanType.SelectedItem = _msDataFileScanHelper.NameFromSource(_msDataFileScanHelper.Source);
                comboBoxScanType.SelectedIndexChanged += comboBoxScanType_SelectedIndexChanged;
                comboBoxScanType.Enabled = true;

                LoadScan(true, true);
            }
            else
            {
                ClearGraph();

                // No full scans can be displayed.
                _graphHelper.SetErrorGraphItem(new UnavailableMSGraphItem());
            }

            UpdateUI(false);
        }

        private void LoadScan(bool zoomXAxis, bool zoomYAxis)
        {
            IsLoaded = false;

            _zoomXAxis = zoomXAxis;
            _zoomYAxis = zoomYAxis;

            int scanId = _msDataFileScanHelper.GetScanIndex();
            if (scanId < 0)
            {
                GraphPane.CurveList.Clear();
                GraphPane.GraphObjList.Clear();
                ClearGraph();
                FireSelectedScanChanged(0);
                return;
            }

            // Display scan id as 1-based to match SeeMS.
            lblScanId.Text = (scanId+1).ToString(@"D");

            RunBackground(LoadingTextIfNoChange);

            var peakType = _msDataFileScanHelper.ParsePeakTypeEnumName(Settings.Default.FullScanPeakType);
            if (peakType == PeakType.chromDefault)
                _msDataFileScanHelper.ScanProvider.SetScanForBackgroundLoad(scanId);
            else
            {
                if (_msDataFileScanHelper.Source == ChromSource.fragment)
                    _msDataFileScanHelper.ScanProvider.SetScanForBackgroundLoad(scanId, null, peakType == PeakType.centroided);
                else
                    _msDataFileScanHelper.ScanProvider.SetScanForBackgroundLoad(scanId, peakType == PeakType.centroided);
            }
        }

        private void RunBackground(Action action)
        {
            var worker = new BackgroundWorker();
            worker.DoWork += (sender, args) => action();
            worker.RunWorkerAsync();
        }

        private void LoadingTextIfNoChange()
        {
            // Only set the title to Loading... when it takes more than 200 miliseconds to get scans
            var fullScans = _msDataFileScanHelper.MsDataSpectra;
            Thread.Sleep(200);
            if (ReferenceEquals(fullScans, _msDataFileScanHelper.MsDataSpectra))
            {
                Invoke(new Action(() =>
                {
                    // Need to check again once on the UI thread
                    if (ReferenceEquals(fullScans, _msDataFileScanHelper.MsDataSpectra))
                    {
                        GraphPane.Title.Text = GraphsResources.GraphFullScan_LoadScan_Loading___;
                        graphControl.Refresh();
                    }
                }));
            }
        }

        /// <summary>
        /// Create the heat map or single scan graph, or both in a dual-pane layout when ion mobility data is present.
        /// </summary>
        private void CreateGraph()
        {
            if (_msDataFileScanHelper.MsDataSpectra == null)
                return;

            if(!toolStripButtonShowAnnotations.Checked)
                _rmis = null;

            bool hasIonMobilityDimension = _msDataFileScanHelper.MsDataSpectra.Length > 1 ||
                                           _msDataFileScanHelper.MsDataSpectra.First().IonMobilities != null;

            toolStripButtonShowAnnotations.Visible = (_msDataFileScanHelper.Source == ChromSource.fragment);
            _showIonSeriesAnnotations = toolStripButtonShowAnnotations.Visible && Settings.Default.ShowFullScanAnnotations;

            if (hasIonMobilityDimension)
            {
                // Is there actually any drift time filtering available?
                double minIonMobilityFilter, maxIonMobilityFilter;
                _msDataFileScanHelper.GetIonMobilityFilterRange(out minIonMobilityFilter, out maxIonMobilityFilter, ChromSource.unknown);

                spectrumBtn.Visible = true;
                filterBtn.Visible = !spectrumBtn.Checked;
                if (filterBtn.Visible && (minIonMobilityFilter == double.MinValue) && (maxIonMobilityFilter == double.MaxValue))
                {
                    filterBtn.Visible = false;
                    filterBtn.Checked = false;
                }

                bool showHeatMap = !spectrumBtn.Checked;

                if (showHeatMap)
                {
                    // Dual-pane: stick spectrum on top, heatmap+mobilogram on bottom
                    bool showMobilogram = mobilogramBtn.Checked;
                    SetupDualPaneLayout(showMobilogram);

                    mobilogramBtn.Visible = true;
                    graphControl.IsEnableVPan = graphControl.IsEnableVZoom = true;
                    _heatMapPane.Legend.IsVisible = true;
                    _heatMapPane.Legend.Position = LegendPos.BottomCenter;
                    _heatMapPane.ShowHeatMap = true;

                    // Clear both panes
                    _stickSpectrumPane.CurveList.Clear();
                    _stickSpectrumPane.GraphObjList.Clear();
                    _heatMapPane.CurveList.Clear();
                    _heatMapPane.GraphObjList.Clear();
                    if (_mobilogramPane != null)
                    {
                        _mobilogramPane.CurveList.Clear();
                        _mobilogramPane.GraphObjList.Clear();
                    }

                    GetRankedSpectrum();

                    // Tight right margin on heatmap so the heatmap chart butts close to the mobilogram
                    _heatMapPane.Margin.Right = showMobilogram ? 2 : ZedGraph.Margin.Default.Right;
                    _stickSpectrumPane.Margin.Right = showMobilogram ? 2 : ZedGraph.Margin.Default.Right;
                    ZoomHeatMapYAxis();
                    CreateIonMobilityHeatmap();
                    if (showMobilogram)
                    {
                        CreateMobilogram();
                        // Force the 2x2 column/row split — earlier paths (toggle off/on +
                        // magnify zoom) can leave panes at default 50/50 widths.
                        AdjustFourPaneColumnWidths();
                    }

                    // Sync stick pane X range from heatmap pane before populating
                    _stickSpectrumPane.XAxis.Scale.Min = _heatMapPane.XAxis.Scale.Min;
                    _stickSpectrumPane.XAxis.Scale.Max = _heatMapPane.XAxis.Scale.Max;
                    _stickSpectrumPane.XAxis.Scale.MinAuto = false;
                    _stickSpectrumPane.XAxis.Scale.MaxAuto = false;

                    // Top pane: stick spectrum (summed across ion mobility)
                    double[] massErrors;
                    CreateSingleScanInPane(_stickSpectrumPane, out massErrors);
                    ZoomStickYAxis();

                    PopulateProperties();

                    AddExtractionBoxes(_heatMapPane);
                    AddExtractionBoxes(_stickSpectrumPane);

                    if (!_showIonSeriesAnnotations)
                        AddTransitionLabels(_stickSpectrumPane, massErrors);

                    double retentionTime = _msDataFileScanHelper.MsDataSpectra[0].RetentionTime ?? _msDataFileScanHelper.ScanProvider.Times[_msDataFileScanHelper.ScanIndex];
                    _stickSpectrumPane.Title.Text = string.Format(Resources.GraphFullScan_CreateGraph__0_____1_F2__min_, _msDataFileScanHelper.FileName, retentionTime);
                    _stickSpectrumPane.Title.IsVisible = true;
                    _heatMapPane.Title.IsVisible = false;

                    FireSelectedScanChanged(retentionTime);
                }
                else
                {
                    // Heatmap off: single-pane stick spectrum (summed across ion mobility)
                    SetupSinglePaneLayout();

                    mobilogramBtn.Visible = false;
                    GraphPane.CurveList.Clear();
                    GraphPane.GraphObjList.Clear();
                    graphControl.IsEnableVPan = graphControl.IsEnableVZoom = false;
                    GraphPane.Legend.IsVisible = false;

                    GetRankedSpectrum();
                    double[] massErrors;
                    CreateSingleScan(out massErrors);

                    PopulateProperties();
                    AddExtractionBoxes(GraphPane);
                    if (!_showIonSeriesAnnotations)
                        AddTransitionLabels(GraphPane, massErrors);

                    double retentionTime = _msDataFileScanHelper.MsDataSpectra[0].RetentionTime ?? _msDataFileScanHelper.ScanProvider.Times[_msDataFileScanHelper.ScanIndex];
                    GraphPane.Title.Text = string.Format(Resources.GraphFullScan_CreateGraph__0_____1_F2__min_, _msDataFileScanHelper.FileName, retentionTime);

                    FireSelectedScanChanged(retentionTime);
                }
            }
            else
            {
                // Single pane: stick spectrum only (no ion mobility data)
                SetupSinglePaneLayout();

                GraphPane.CurveList.Clear();
                GraphPane.GraphObjList.Clear();

                spectrumBtn.Visible = false;
                filterBtn.Visible = false;
                mobilogramBtn.Visible = false;
                graphControl.IsEnableVPan = graphControl.IsEnableVZoom = false;
                GraphPane.Legend.IsVisible = false;

                GetRankedSpectrum();
                double[] massErrors;
                CreateSingleScan(out massErrors);

                PopulateProperties();
                AddExtractionBoxes(GraphPane);
                if (!_showIonSeriesAnnotations)
                    AddTransitionLabels(GraphPane, massErrors);

                double retentionTime = _msDataFileScanHelper.MsDataSpectra[0].RetentionTime ?? _msDataFileScanHelper.ScanProvider.Times[_msDataFileScanHelper.ScanIndex];
                GraphPane.Title.Text = string.Format(Resources.GraphFullScan_CreateGraph__0_____1_F2__min_, _msDataFileScanHelper.FileName, retentionTime);

                FireSelectedScanChanged(retentionTime);
            }
        }

        private BoxObj CreateExtractionBox(TransitionFullScanInfo transition)
        {
            var color1 = GraphHelper.Blend(GetTransitionColor(transition), Color.White, 0.60);
            var color2 = GraphHelper.Blend(GetTransitionColor(transition), Color.White, 0.95);
            var extractionBox = new BoxObj(
                transition.ProductMz - transition.ExtractionWidth.Value / 2,
                0.0,
                transition.ExtractionWidth.Value,
                1.0,
                Color.Transparent,
                transition.Color,
                Color.White)
            {
                Location = { CoordinateFrame = CoordType.XScaleYChartFraction },
                ZOrder = ZOrder.F_BehindGrid,
                Fill = new Fill(color1, color2, 90),
                IsClippedToChartRect = true,
            };
            extractionBox.Tag = new ExtractionBoxInfo(transition.ProductMz, transition.ExtractionWidth.Value);
            return extractionBox;
        }

        private void AddExtractionBoxes(MSGraphPane targetPane)
        {
            for (int i = 0; i < _msDataFileScanHelper.ScanProvider.Transitions.Length; i++)
            {
                var transition = _msDataFileScanHelper.ScanProvider.Transitions[i];
                if (transition.Source != _msDataFileScanHelper.Source)
                    continue;
                targetPane.GraphObjList.Add(CreateExtractionBox(transition));
            }
        }

        private void AddTransitionLabels(MSGraphPane targetPane, double[] massErrors)
        {
            var showMassError = Settings.Default.ShowFullScanMassError;
            for (int i = 0; i < _msDataFileScanHelper.ScanProvider.Transitions.Length; i++)
            {
                var transition = _msDataFileScanHelper.ScanProvider.Transitions[i];
                if (transition.Source != _msDataFileScanHelper.Source)
                    continue;
                var labelBuilder = new StringBuilder(transition.Name);
                if (massErrors != null && showMassError)
                {
                    var massError = SequenceMassCalc.GetPpm(transition.ProductMz, massErrors[i]);
                    massError = Math.Round(massError, 1);
                    labelBuilder.AppendLine().Append(string.Format(Resources.GraphSpectrum_MassErrorFormat_ppm,
                        (massError > 0 ? @"+" : string.Empty), massError));
                }

                var label = new TextObj(labelBuilder.ToString(), transition.ProductMz, 0.02, CoordType.XScaleYChartFraction,
                    AlignH.Center, AlignV.Top)
                {
                    ZOrder = ZOrder.D_BehindAxis,
                    IsClippedToChartRect = true,
                    Tag = i
                };
                label.FontSpec.Border.IsVisible = false;
                label.FontSpec.FontColor = GraphHelper.Blend(GetTransitionColor(transition), Color.Black, 0.30);
                label.FontSpec.IsBold = true;
                label.FontSpec.Fill = new Fill(Color.FromArgb(180, Color.White));
                targetPane.GraphObjList.Add(label);
            }
        }

        /// <summary>
        /// Create an ion mobility heat map graph.
        /// </summary>
        private void CreateIonMobilityHeatmap()
        {
            var isWatersSonarData = _msDataFileScanHelper.IsWatersSonarData;
            GraphPane.YAxis.Title.Text = isWatersSonarData ?
                Resources.GraphFullScan_CreateIonMobilityHeatmap_Quadrupole_Scan_Range__m_z_ :
                IonMobilityFilter.IonMobilityUnitsL10NString(_msDataFileScanHelper.IonMobilityUnits);
            graphControl.IsEnableVZoom = graphControl.IsEnableVPan = true;

            if (_heatMapData == null)
            {
                var points = new List<Point3D>(5000);
                foreach (var scan in _msDataFileScanHelper.MsDataSpectra)
                {
                    if (!scan.IonMobility.HasValue && scan.IonMobilities == null)
                        continue;
                    for (int j = 0; j < scan.Mzs.Length; j++)
                    {
                        double mobilityValue = scan.IonMobilities != null
                            ? scan.IonMobilities[j]
                            : scan.IonMobility.Mobility.Value;
                        if (isWatersSonarData)
                        {
                            // "mobility" value is actually a bin number, convert to scanning quad m/z filter value
                            mobilityValue = _msDataFileScanHelper.ScanProvider.SonarBinToPrecursorMz((int)mobilityValue) ?? 0;
                            Assume.AreNotEqual(mobilityValue,0, @"unexpected bin number in SONAR data");
                        }
                        points.Add(new Point3D(scan.Mzs[j],  mobilityValue, scan.Intensities[j]));
                    }
                }
                _heatMapData = new HeatMapData(points, mobilogramBtn.Checked,
                    ((IHeatMapDataProvider)_heatMapPane).HeatMapZAxisName);
            }

            double minDrift;
            double maxDrift;

            _msDataFileScanHelper.GetIonMobilityFilterDisplayRange(out minDrift, out maxDrift, _msDataFileScanHelper.Source);  // There may be a different drift time filter for products in Waters
            if (minDrift > 0 && maxDrift < double.MaxValue)
            {
                // Add gray shaded box behind heat points.
                var driftTimeBox = new BoxObj(
                    0.0,
                    maxDrift,
                    1.0,
                    maxDrift - minDrift,
                    Color.Transparent,
                    Color.FromArgb(50, Color.Gray))
                {
                    Location = {CoordinateFrame = CoordType.XChartFractionYScale},
                    ZOrder = ZOrder.F_BehindGrid,
                    IsClippedToChartRect = true,
                    Tag = TAG_IM_FILTER_BAND
                };
                GraphPane.GraphObjList.Add(driftTimeBox);

                // Add outline in front of heat points, so you can tell where the limits are in a dense graph.
                var driftTimeOutline = new BoxObj(
                    0.0,
                    maxDrift,
                    1.0,
                    maxDrift - minDrift,
                    Color.FromArgb(50, Color.DarkViolet),
                    Color.Transparent)
                {
                    Location = {CoordinateFrame = CoordType.XChartFractionYScale},
                    ZOrder = ZOrder.C_BehindChartBorder,
                    IsClippedToChartRect = true,
                    Border = new Border(Color.FromArgb(100, Color.DarkViolet), 2),
                    Tag = TAG_IM_FILTER_BAND
                };
                GraphPane.GraphObjList.Add(driftTimeOutline);

                // Add dashed line at peak center
                double? peakCenter = null;
                if (isWatersSonarData)
                {
                    // For SONAR, the Y-axis is precursor m/z — show the selected precursor's m/z
                    var currentTransition = _msDataFileScanHelper.CurrentTransition;
                    if (currentTransition != null)
                        peakCenter = currentTransition.PrecursorMz;
                }
                else
                {
                    // For IM data, show the ion mobility center (accounts for high-energy offset)
                    var imFilter = _msDataFileScanHelper.CurrentTransition?.IonMobilityInfo;
                    if (imFilter != null && imFilter.HasIonMobilityValue)
                        peakCenter = imFilter.IonMobility.Mobility.Value +
                                     (imFilter.IonMobilityFilterWindow.Offset);
                }
                if (peakCenter.HasValue)
                {
                    var centerLine = new LineObj(
                        Color.FromArgb(180, Color.DarkViolet),
                        0.0, peakCenter.Value, 1.0, peakCenter.Value)
                    {
                        Location = { CoordinateFrame = CoordType.XChartFractionYScale },
                        ZOrder = ZOrder.C_BehindChartBorder,
                        IsClippedToChartRect = true,
                        Line =
                        {
                            Style = System.Drawing.Drawing2D.DashStyle.Dash,
                            Width = 1.5f
                        }
                    };
                    GraphPane.GraphObjList.Add(centerLine);
                }
            }

            if (!Settings.Default.FilterIonMobilityFullScan)
            {
                minDrift = 0;
                maxDrift = double.MaxValue;
            }
            var heatMapGraphPane = (HeatMapGraphPane)GraphPane;
            heatMapGraphPane.SetPoints(_heatMapData, minDrift, maxDrift);
        }

        private void PopulateProperties()
        {
            bool hasIonMobilityDimension = _msDataFileScanHelper.MsDataSpectra.Length > 1 ||
                                           _msDataFileScanHelper.MsDataSpectra.First().IonMobilities != null;

            var spectra = _msDataFileScanHelper.MsDataSpectra;
            FullScanProperties spectrumProperties = null;
            if (spectra.Any())
            {
                spectrumProperties = FullScanProperties.CreateProperties(spectra[0]);

                MsPrecursor spectrumPrecursor = _msDataFileScanHelper.MsDataSpectra.SelectMany(spectrum => spectrum.Precursors).LastOrDefault();
                if (spectrumPrecursor.PrecursorCollisionEnergy != null)
                    spectrumProperties.CE = spectrumPrecursor.PrecursorCollisionEnergy.Value.ToString(Formats.OPT_PARAMETER);

                var transition = _msDataFileScanHelper.CurrentTransition;
                if (transition != null)
                {
                    if (transition.IonMobilityInfo != null && transition.IonMobilityInfo.IonMobilityAndCCS != null)
                    {
                        var imAndCss = transition.IonMobilityInfo.IonMobilityAndCCS;
                        if (imAndCss.HasIonMobilityValue)
                        {
                            // Use window center as the effective IM (accounts for high-energy offset in product transitions)
                            var mobility = imAndCss.IonMobility.Mobility.Value +
                                           (transition.IonMobilityInfo.IonMobilityFilterWindow.Offset);
                            spectrumProperties.IonMobility = TextUtil.SpaceSeparate(mobility.ToString(Formats.IonMobility),
                                imAndCss.IonMobility.UnitsString);
                        }
                        if(imAndCss.HasCollisionalCrossSection)
                            spectrumProperties.CCS = imAndCss.CollisionalCrossSectionSqA.Value.ToString(Formats.CCS);
                    }
                }
                if (hasIonMobilityDimension)
                {
                    double minIonMobilityFilter, maxIonMobilityFilter;
                    var fullScans = _msDataFileScanHelper.GetFilteredScans(out minIonMobilityFilter, out maxIonMobilityFilter); // Get range of IM values for all products and precursors

                    var ionMobilityMin = double.MaxValue;
                    var ionMobilityMax = double.MinValue;
                    foreach (var scan in fullScans)
                    {
                        var mobility = scan.MinIonMobility ?? scan.IonMobility?.Mobility;
                        if (mobility.HasValue)
                            ionMobilityMin = Math.Min(ionMobilityMin, mobility.Value);
                        mobility = scan.MaxIonMobility ?? scan.IonMobility?.Mobility;
                        if (mobility.HasValue)
                            ionMobilityMax = Math.Max(ionMobilityMax, mobility.Value);
                    }
                    spectrumProperties.IonMobilityRange = TextUtil.AppendColon(ionMobilityMin.ToString(Formats.IonMobility)) + ionMobilityMax.ToString(Formats.IonMobility);
                    if(_msDataFileScanHelper.GetIonMobilityFilterDisplayRange(out minIonMobilityFilter, out maxIonMobilityFilter, ChromSource.unknown))
                        spectrumProperties.IonMobilityFilterRange = TextUtil.AppendColon(minIonMobilityFilter.ToString(Formats.IonMobility)) + maxIonMobilityFilter.ToString(Formats.IonMobility);
                    spectrumProperties.DataPoints = fullScans.Select(scan => scan.Intensities.Length).Sum().ToString(@"N0");
                    spectrumProperties.MzCount = fullScans.SelectMany(scan => scan.Mzs).Distinct().Count().ToString(@"N0");
                    
                    if(fullScans.Any(scan => scan.IonMobilities != null))
                        spectrumProperties.IonMobilityCount = fullScans.Where(scan => scan.IonMobilities != null)
                            .Select(scan => scan.IonMobilities.Distinct().Count()).Sum().ToString(@"N0");

                    if(_msDataFileScanHelper.MsDataSpectra.Length > 1)
                        spectrumProperties.ScanId = TextUtil.SpaceSeparate(_msDataFileScanHelper.MsDataSpectra[0].Id, @"-", _msDataFileScanHelper.MsDataSpectra.Last().Id);
                    else
                        spectrumProperties.ScanId = _msDataFileScanHelper.MsDataSpectra[0].Id; 

                    if (_msDataFileScanHelper.CurrentTransition?.IonMobilityInfo?.HighEnergyIonMobilityOffset != null)
                        spectrumProperties.HighEnergyOffset = _msDataFileScanHelper.CurrentTransition?.IonMobilityInfo?.HighEnergyIonMobilityOffset.ToString();
                }
                else
                {
                    spectrumProperties.MzCount = spectra[0].Mzs.Length.ToString(@"N0");

                    var parts = _msDataFileScanHelper.MsDataSpectra[0].Id.Split('.'); // Check for merge.frame.start.stop from 3-array IMS data
                    var id = parts.Length < 4
                        ? _msDataFileScanHelper.MsDataSpectra[0].Id
                        : string.Format(@"{0}.{1}-{0}.{2}", parts[1], parts[2], parts[3]);
                    var ionMobility = _msDataFileScanHelper.MsDataSpectra[0].IonMobility;
                    spectrumProperties.ScanId = id;
                    if (ionMobility.HasValue)
                        spectrumProperties.IonMobility = ionMobility.ToString();
                }
                if (_msDataFileScanHelper.MsDataSpectra.Any(scan=>scan.Metadata.InjectionTime.HasValue))
                {
                    var injectionTime = _msDataFileScanHelper.MsDataSpectra.Sum(scan => scan.Metadata.InjectionTime);
                    spectrumProperties.InjectionTime = injectionTime.Value.ToString(@"0.####", CultureInfo.CurrentCulture);
                }

                if (_msDataFileScanHelper.MsDataSpectra.Any(scan => scan.Metadata.TotalIonCurrent.HasValue))
                {
                    spectrumProperties.TotalIonCurrent = _msDataFileScanHelper.MsDataSpectra
                        .Sum(scan => scan.Metadata.TotalIonCurrent ?? 0.0).ToString(Formats.PEAK_AREA);
                }

                if (_documentContainer is SkylineWindow stateProvider)
                {
                    var chromSet = stateProvider.DocumentUI.Settings.MeasuredResults?.Chromatograms.FirstOrDefault(
                        chrom => chrom.ContainsFile(_msDataFileScanHelper.ScanProvider.DataFilePath));
                    spectrumProperties.ReplicateName = chromSet?.Name;
                    if (_peaks?.Length > 0)
                    {
                        var nodePath = DocNodePath.GetNodePath(_msDataFileScanHelper.CurrentTransition?.Id,
                            _documentContainer.DocumentUI);
                        //if current transition is deleted look up precursor from any of the transitions in the graph
                        if(nodePath == null)
                        {
                            foreach (var t in _msDataFileScanHelper.ScanProvider.Transitions)
                            {
                                nodePath = DocNodePath.GetNodePath(t.Id, _documentContainer.DocumentUI);
                                if (nodePath != null)
                                    break;
                            }
                        }

                        if (nodePath !=null)
                        {
                            if (!ReferenceEquals(nodePath.Precursor, _precursor?.DocNode))
                            {
                                _precursor = new GraphSpectrum.Precursor(_documentContainer.DocumentUI.Settings, null,
                                    nodePath.Peptide, nodePath.Precursor);
                            }

                            // expectedSpectrum to docTransitions join
                            var thisSpectrumHash = GetPeakIntensities(
                                _msDataFileScanHelper.ScanProvider.Transitions.ToList(), stateProvider.DocumentUI);

                            var isMs1 = _msDataFileScanHelper.Source == ChromSource.ms1;
                            if (_precursor.Spectra?.Count > 0)
                            {
                                if (_precursor.DocNode.Transitions.Count(t => t.IsMs1 == isMs1) > 1)
                                {
                                    var dotpList = (
                                        from peakDoc in _precursor.DocNode.Transitions
                                        join peakSpec in thisSpectrumHash on ReferenceValue.Of(peakDoc.Id) equals peakSpec.Key
                                        where peakDoc.IsMs1 == isMs1 && (peakDoc.HasLibInfo || peakDoc.IsMs1)
                                        select new
                                        {
                                            expected = peakDoc.IsMs1
                                                ? peakDoc.IsotopeDistInfo.Proportion
                                                : peakDoc.LibInfo.Intensity,
                                            actual = peakSpec.Value
                                        }).ToList();

                                    if (dotpList.Count > 1)
                                    {
                                        var dotp = new Statistics(dotpList.Select(d => (double)d.expected))
                                            .NormalizedContrastAngleSqrt(new Statistics(
                                                dotpList.Select(d => d.actual))).ToString(Formats.PEAK_FOUND_RATIO);
                                        if (isMs1)
                                            spectrumProperties.idotp = dotp;
                                        else
                                            spectrumProperties.dotp = dotp;
                                    }
                                }
                            }
                        }                    }
                }
            }

            // avoid control refresh if there are no changes
            if (graphControlExtension.PropertiesSheet.SelectedObject == null || graphControlExtension.PropertiesSheet.SelectedObject is FullScanProperties currentProps && !currentProps.IsSameAs(spectrumProperties))
                graphControlExtension.PropertiesSheet.SelectedObject = spectrumProperties;
        }

        private Dictionary<ReferenceValue<Identity>, double> GetPeakIntensities(
            List<TransitionFullScanInfo> transitions, SrmDocument document)
        {
            
            var docTransitions = CollectionUtil.SafeToDictionary(
                transitions.Select(t => new KeyValuePair<SignedMz, TransitionFullScanInfo>(t.ProductMz, t)));

            var isNegative = (_precursor.DocNode.Id as TransitionGroup)?.PrecursorAdduct.AdductCharge < 0;
            var signedQ1FilterValues = docTransitions.Select(q => q.Key).ToList();
            var key = new PrecursorTextId(_precursor.DocNode.PrecursorMz, null, null, null, null, ChromExtractor.summed);
            var filter = new SpectrumFilterPair(key, PeptideDocNode.UNKNOWN_COLOR, 0, null, null, false, false);
            filter.AddQ1FilterValues(signedQ1FilterValues, mz =>
            {
                docTransitions.TryGetValue(new SignedMz(mz, isNegative), out var transitionFullScanInfo);
                return transitionFullScanInfo?.ExtractionWidth ??
                       document.Settings.TransitionSettings.FullScan.GetProductFilterWindow(mz);
            });

            // extract peak intensities for the document fragments from the current spectrum
            var expectedSpectrum = filter.FilterQ1SpectrumList(new[] { new MsDataSpectrum
                { Mzs = _peaks.Select(p => p.Mz).ToArray(), Intensities = _peaks.Select(p => (double)p.Intensity).ToArray(), NegativeCharge = isNegative } });

            // expectedSpectrum to docTransitions join
            var thisSpectrumHash = Enumerable.Range(0, expectedSpectrum.ProductFilters.Length)
                .Select(i => new { mz = expectedSpectrum.ProductFilters[i].TargetMz, intensity = (double)expectedSpectrum.Intensities[i] })
                .Where(d => docTransitions.ContainsKey(d.mz) && docTransitions[d.mz]?.Id is Transition)
                .ToDictionary(d => ReferenceValue.Of(docTransitions[d.mz].Id), d => d.intensity);

            return thisSpectrumHash;
        }

        private class RankingContext
        {
            public int scanIndex;
            public TransitionGroupDocNode precursor;
            public ImmutableList<IonType> types;
            public ImmutableList<int> charges;
            public ImmutableList<Adduct> rankAdducts;
            public ImmutableList<IonType> rankTypes;

            public override bool Equals(object obj)
            {
                if (obj is RankingContext other)
                    return scanIndex.Equals(other.scanIndex)
                           && ReferenceEquals(precursor.Id, other.precursor.Id) 
                           && types.Equals(other.types)
                           && charges.Equals(other.charges)
                           && rankAdducts.Equals(
                               other.rankAdducts) &&
                           rankTypes.Equals(other.rankTypes);
                else
                    return false;
            }

            public override int GetHashCode()
            {
                var res = scanIndex.GetHashCode();
                res = res * 397 ^ precursor.Id.GetHashCode();
                res = res * 397 ^ types.GetHashCode();
                res = res * 397 ^ charges.GetHashCode();
                res = res * 397 ^ rankAdducts.GetHashCode();
                res = res * 397 ^ rankTypes.GetHashCode();
                return res;
            }
        }

        private RankingContext rankContext { get; set; }

        private SpectrumGraphItem RankScan(IList<double> mzs, IList<double> intensities, SrmSettings settings, 
            TransitionGroupDocNode precursor, TransitionDocNode transitionNode)
        {
            if (!(_documentContainer is SkylineWindow)) return null;
            var stateProvider = (GraphSpectrum.IStateProvider) _documentContainer;
            var group = precursor.TransitionGroup;
            var types = stateProvider.ShowIonTypes(group.IsProteomic);
            var losses = stateProvider.ShowLosses();
            var adducts =
                (group.IsProteomic
                    ? Transition.DEFAULT_PEPTIDE_LIBRARY_CHARGES
                    : precursor.InUseAdducts).ToArray();
            var charges = stateProvider.ShowIonCharges(adducts);
            var rankTypes = group.IsProteomic
                ? settings.TransitionSettings.Filter.PeptideIonTypes
                : settings.TransitionSettings.Filter.SmallMoleculeIonTypes;
            var rankAdducts = group.IsProteomic
                ? settings.TransitionSettings.Filter.PeptideProductCharges
                : settings.TransitionSettings.Filter.SmallMoleculeFragmentAdducts;
            var rankCharges = Adduct.OrderedAbsoluteChargeValues(rankAdducts);

            int i = 0;
            foreach (IonType type in rankTypes)
            {
                if (types.Remove(type))
                    types.Insert(i++, type);
            }

            i = 0;
            var showAdducts = new List<Adduct>();
            foreach (var charge in rankCharges)
            {
                if (charges.Remove(charge))
                    charges.Insert(i++, charge);
                // NB for all adducts we just look at abs value of charge
                // CONSIDER(bspratt): we may want finer per-adduct control for small molecule use
                showAdducts.AddRange(adducts.Where(a => charge == Math.Abs(a.AdductCharge)));
            }

            showAdducts.AddRange(adducts.Where(a =>
                charges.Contains(Math.Abs(a.AdductCharge)) && !showAdducts.Contains(a)));

            var precursorNodePath = DocNodePath.GetNodePath(precursor.Id, _documentContainer.DocumentUI);
            if (precursorNodePath != null) 
            {
                var spectrumInfo = new SpectrumPeaksInfo(Enumerable.Range(0, mzs.Count).Select(j => new SpectrumPeaksInfo.MI()
                    {Mz = mzs[j], Intensity = (float)intensities[j]}).ToArray());

                var newRankingContext = new RankingContext(){scanIndex = _msDataFileScanHelper.ScanIndex, precursor = precursor,
                    types = ImmutableList.ValueOf(types), charges = ImmutableList.ValueOf(charges),
                    rankTypes = ImmutableList.ValueOf(rankTypes), rankAdducts = ImmutableList.ValueOf(rankAdducts)};

                if (_rmis == null || rankContext == null || !rankContext.Equals(newRankingContext) || !_rmis.Tolerance.Equals(settings.TransitionSettings.Libraries.IonMatchMzTolerance))
                {
                    rankContext = newRankingContext;
                    _rmis = LibraryRankedSpectrumInfo.NewLibraryRankedSpectrumInfo(spectrumInfo,
                        precursor.LabelType,
                        precursor,
                        settings,
                        precursorNodePath.Peptide.SourceUnmodifiedTarget,
                        precursorNodePath.Peptide.SourceExplicitMods,
                        showAdducts,
                        types,
                        rankAdducts,
                        rankTypes,
                        null);
                    _rmis = _rmis.ChangeScore(null);
                    //create a map of transition indices vs. peak rank to use in the mouse click and mouse move handlers.
                    if (!_rmis.PeaksRanked.IsNullOrEmpty())
                    {
                        _transitionIndex = Enumerable
                            .Repeat(-1, Enumerable.Max(_rmis.PeaksRanked.Select(p => p.Rank)) + 1).ToArray();
                        foreach (var rankedPeak in _rmis.PeaksRanked)
                        {
                            var transitions = _msDataFileScanHelper.ScanProvider.Transitions;
                            var tIndex = -1;
                            for (var j = 0; j < transitions.Length; j++)
                            {
                                var t = (transitions[j].Id as Transition);
                                if (rankedPeak.MatchedIons.Any(ion =>
                                    ion.IonType == t?.IonType && ion.Ordinal == t.Ordinal &&
                                    ion.Charge.AdductCharge == t.Charge))
                                {
                                    tIndex = j;
                                    break;
                                }
                            }

                            _transitionIndex[rankedPeak.Rank] = tIndex;
                        }
                    }
                    else
                        _transitionIndex = new int[]{};
                }

                var isHighRes = !Equals(settings.TransitionSettings.FullScan.ProductMassAnalyzer,FullScanMassAnalyzerType.qit);

                var graphItem = new SpectrumGraphItem(precursorNodePath.Peptide, precursor, transitionNode, _rmis, "")
                {
                    ShowTypes = types,
                    ShowCharges = charges,
                    ShowLosses = losses,
                    ShowRanks = Settings.Default.ShowRanks,
                    ShowMz = Settings.Default.ShowIonMz,
                    ShowObservedMz = Settings.Default.ShowObservedMz,
                    ShowMassError = Settings.Default.ShowFullScanMassError,
                    ShowDuplicates = Settings.Default.ShowDuplicateIons,
                    FontSize = Settings.Default.SpectrumFontSize,
                    LineWidth = Settings.Default.SpectrumLineWidth
                };

                return graphItem;
            }
            return null;
        }

        private void GetRankedSpectrum()
        {
            var spectra = _msDataFileScanHelper.MsDataSpectra;

            IList<double> mzs;
            IList<double> intensities;

            if (spectra.Length == 1 && spectra[0].IonMobilities == null)
            {
                mzs = spectra[0].Mzs;
                intensities = spectra[0].Intensities;
            }
            else
            {
                // Ion mobility being shown as 2-D spectrum
                mzs = new List<double>();
                intensities = new List<double>();

                var fullScans = _msDataFileScanHelper.GetFilteredScans(out var ionMobilityFilterMin, out var ionMobilityFilterMax);

                double minMz;
                var indices = new int[fullScans.Length];
                while ((minMz = FindMinMz(fullScans, indices)) < double.MaxValue)
                {
                    mzs.Add(minMz);
                    intensities.Add(SumIntensities(fullScans, minMz, indices, ionMobilityFilterMin, ionMobilityFilterMax));
                }
            }

            // Save the full spectrum for later use in dotp calculation
            _peaks = new SpectrumPeaksInfo.MI[mzs.Count];
            for (int i = 0; i < _peaks.Length; i++)
                _peaks[i] = new SpectrumPeaksInfo.MI() { Mz = mzs[i], Intensity = (float)intensities[i] };

            if (_msDataFileScanHelper.Source == ChromSource.fragment)
            {
                var nodePath = DocNodePath.GetNodePath(_msDataFileScanHelper.CurrentTransition?.Id,
                    _documentContainer.DocumentUI);

                if (nodePath != null) // Make sure user hasn't removed node since last update
                {
                    var graphItem = RankScan(mzs, intensities, _documentContainer.DocumentUI.Settings,
                        nodePath.Precursor, nodePath.Transition);
                }
            }
        }

        /// <summary>
        /// Create stick graph of a single scan.
        /// </summary>
        private void CreateSingleScan(out double[] massErrors)
        {
            CreateSingleScanInPane(GraphPane, out massErrors);
            graphControl.IsEnableVZoom = graphControl.IsEnableVPan = false;
        }

        /// <summary>
        /// Create stick graph of a single scan in a specific pane.
        /// </summary>
        private void CreateSingleScanInPane(MSGraphPane targetPane, out double[] massErrors)
        {
            targetPane.YAxis.Title.Text = GraphsResources.AbstractMSGraphItem_CustomizeYAxis_Intensity;
            massErrors = null;

            // Create a point list for each transition, and a default point list for points not 
            // associated with a transition.
            var pointLists = new PointPairList[_msDataFileScanHelper.ScanProvider.Transitions.Length];
            for (int i = 0; i < pointLists.Length; i++)
                pointLists[i] = new PointPairList();
            var defaultPointList = new PointPairList();
            var allPointList = new PointPairList();

            // Assign each point to a transition point list, or else the default point list.
            IList<double> mzs;
            IList<double> intensities;
            bool negativeScan;
            var spectra = _msDataFileScanHelper.MsDataSpectra;

            if (spectra.Length == 1 && spectra[0].IonMobilities == null)
            {
                mzs = spectra[0].Mzs;
                intensities = spectra[0].Intensities;
                negativeScan = spectra[0].NegativeCharge;
            }
            else
            {
                // Ion mobility being shown as 2-D spectrum
                mzs = new List<double>();
                intensities = new List<double>();

                var fullScans = _msDataFileScanHelper.GetFilteredScans(out var ionMobilityFilterMin, out var ionMobilityFilterMax);
                negativeScan = fullScans.Any() && fullScans.First().NegativeCharge;

                double minMz;
                var indices = new int[fullScans.Length];
                while ((minMz = FindMinMz(fullScans, indices)) < double.MaxValue)
                {
                    mzs.Add(minMz);
                    intensities.Add(SumIntensities(fullScans, minMz, indices, ionMobilityFilterMin, ionMobilityFilterMax));
                }
            }

            for (int i = 0; i < mzs.Count; i++)
            {
                double mz = mzs[i];
                double intensity = intensities[i];
                allPointList.Add(mz, intensity);
                var assignedPointList = defaultPointList;
                for (int j = 0; j < _msDataFileScanHelper.ScanProvider.Transitions.Length; j++)
                {
                    var transition = _msDataFileScanHelper.ScanProvider.Transitions[j];
                    // Polarity should match, because these are the spectra used for extraction
                    Assume.IsTrue(transition.PrecursorMz.IsNegative == negativeScan);
                    if (transition.Source != _msDataFileScanHelper.Source ||
                        !transition.MatchMz(mz))
                        continue;
                    assignedPointList = pointLists[j];
                    break;
                }
                assignedPointList.Add(mz, intensity);
            }

            GraphSpectrum.SpectrumNodeSelection selection = null;
            var selectionMatch = false;
            if (_documentContainer is SkylineWindow stateProvider)
            {
                selection = GraphSpectrum.SpectrumNodeSelection.GetCurrent(stateProvider);
                //find out if the current selection belongs to the same precursor as the loaded MS spectrum
                var dataPrecursor = _msDataFileScanHelper.ScanProvider.Transitions.FirstOrDefault(t => (t.Id as Transition)?.IonType == IonType.precursor);
                selectionMatch = ReferenceEquals(selection.NodeTranGroup?.Id, (dataPrecursor?.Id as Transition)?.Group);
            }

            if (_showIonSeriesAnnotations && _msDataFileScanHelper.Source == ChromSource.fragment)
            {
                var nodePath = DocNodePath.GetNodePath(_msDataFileScanHelper.CurrentTransition?.Id, _documentContainer.DocumentUI);

                if (nodePath != null) // Make sure user hasn't removed node since last update
                {
                    var graphItem = RankScan(mzs, intensities, _documentContainer.DocumentUI.Settings, nodePath.Precursor,
                        selectionMatch ? selection.NodeTran : null);
                    if (IsDualPane)
                    {
                        var curveItem = graphControl.AddGraphItem(targetPane, graphItem, false);
                        curveItem.Label.IsVisible = false;
                    }
                    else
                    {
                        _graphHelper.AddSpectrum(graphItem, false);
                    }
                }

                else
                {
                    // No node to use for annotation so just show peaks in gray
                    var item = new SpectrumItem(allPointList, Color.Gray, @"unmatched");
                    var curveItem = _graphHelper.GraphControl.AddGraphItem(targetPane, item, false);
                    curveItem.Label.IsVisible = false;
                }
            }
            else
            {
                // Create a graph item for each point list with its own color.
                for (int i = 0; i < pointLists.Length; i++)
                {
                    var transition = _msDataFileScanHelper.ScanProvider.Transitions[i];
                    if (transition.Source != _msDataFileScanHelper.Source)
                        continue;
                    var item = new SpectrumItem(pointLists[i], GetTransitionColor(transition), _msDataFileScanHelper.ScanProvider.Transitions[i].Name, 2);
                    var curveItem = _graphHelper.GraphControl.AddGraphItem(targetPane, item, false);
                    curveItem.Label.IsVisible = false;
                }

                // Add points that aren't associated with a transition.
                {
                    var item = new SpectrumItem(defaultPointList, Color.Gray, @"unmatched");
                    var curveItem = _graphHelper.GraphControl.AddGraphItem(targetPane, item, false);
                    curveItem.Label.IsVisible = false;
                }
            }
            // Create curve for all points to provide shading behind stick graph.
            if (_msDataFileScanHelper.MsDataSpectra.Length > 0 && !_msDataFileScanHelper.MsDataSpectra[0].Centroided)
            {
                var item = new SpectrumShadeItem(allPointList, Color.FromArgb(100, 225, 225, 150), @"all");
                var curveItem = _graphHelper.GraphControl.AddGraphItem(targetPane, item, false);
                curveItem.Label.IsVisible = false;
            }
            targetPane.SetScale(CreateGraphics());

            if (Settings.Default.ShowFullScanMassError)
            {
                massErrors = new double[_msDataFileScanHelper.ScanProvider.Transitions.Length];

                //create and initialize a map of transition,accumulator pairs
                var meanErrorsMap =
                    new Dictionary<ReferenceValue<Identity>, IntensityAccumulator>(_msDataFileScanHelper.ScanProvider.Transitions.Length);
                _msDataFileScanHelper.ScanProvider.Transitions
                    .ForEach(t => meanErrorsMap.Add(t.Id,
                        new IntensityAccumulator(true, ChromExtractor.summed, t.ProductMz)));

                for (int i = 0; i < mzs.Count; i++)     //accumulate errors for each spectrum point
                {
                    _msDataFileScanHelper.ScanProvider.Transitions.ToList()
                        .FindAll(t => t.Source == _msDataFileScanHelper.Source && t.MatchMz(mzs[i]))
                        .ForEach(t => meanErrorsMap[t.Id].AddPoint(mzs[i], intensities[i]));
                }
                //move results to the output array
                for (int i = 0; i < _msDataFileScanHelper.ScanProvider.Transitions.Length; i++)
                    massErrors[i] = meanErrorsMap[_msDataFileScanHelper.ScanProvider.Transitions[i].Id].MeanMassError;
            }
        }

        private static double FindMinMz(MsDataSpectrum[] spectra, int[] indices)
        {
            double minMz = double.MaxValue;
            for (int i = 0; i < indices.Length; i++)
            {
                var scan = spectra[i];
                int indexMz = indices[i];
                if (indexMz < scan.Mzs.Length)
                    minMz = Math.Min(minMz, spectra[i].Mzs[indexMz]);
            }
            return minMz;
        }

        private static double SumIntensities(MsDataSpectrum[] spectra, double mz, int[] indices, double ionMobilityFilterMin, double ionMobilityFilterMax)
        {
            double intensity = 0;
            for (int i = 0; i < indices.Length; i++)
            {
                var scan = spectra[i];
                int indexMz;
                // Sometimes spectra have multiple intensities for a given m/z.  Sum all intensities for that m/z
                for (indexMz = indices[i]; indexMz < scan.Mzs.Length && scan.Mzs[indexMz] == mz; indexMz++)
                {
                    if (scan.IonMobilities != null)
                    {
                        if (scan.IonMobilities[indexMz] < ionMobilityFilterMin || scan.IonMobilities[indexMz] > ionMobilityFilterMax)
                        {
                            continue;
                        }
                    }
                    intensity += scan.Intensities[indexMz];
                }
                indices[i] = indexMz;
            }
            return intensity;
        }

        private void GetMaxMzIntensity(out double maxMz, out double maxIntensity)
        {
            var fullScans = _msDataFileScanHelper.GetFilteredScans(out var minIonMobilityVal, out var maxIonMobilityVal);
            maxMz = 0;
            maxIntensity = 0;

            double minMz;
            var indices = new int[fullScans.Length];
            while ((minMz = FindMinMz(fullScans, indices)) < double.MaxValue)
            {
                maxMz = Math.Max(maxMz, minMz);
                double intensity = SumIntensities(fullScans, minMz, indices, minIonMobilityVal, maxIonMobilityVal);
                maxIntensity = Math.Max(maxIntensity, intensity);
            }
        }

        private void GetIonMobilityRange(out double minIonMobility, out double maxIonMobility)
        {
            minIonMobility = double.MaxValue;
            maxIonMobility = 0;
            foreach (var spectrum in _msDataFileScanHelper.MsDataSpectra)
            {
                var spectrumMinMobility = Math.Abs(spectrum.MinIonMobility ?? spectrum.IonMobility.Mobility ?? 0);
                var spectrumMaxMobility = Math.Abs(spectrum.MaxIonMobility ?? spectrum.IonMobility.Mobility ?? 0);
                if (_msDataFileScanHelper.IsWatersSonarData)
                {
                    // For Waters SONAR data we transform the fictitious drift dimension (reported as bin number) into what it really is, precursor m/z
                    var bin = (int)spectrumMaxMobility;
                    var sonarBinToPrecursorMz = _msDataFileScanHelper.ScanProvider.SonarBinToPrecursorMz(bin);
                    Assume.IsTrue(sonarBinToPrecursorMz.HasValue, @"error determining m/z value for SONAR bin #" + bin);
                    spectrumMaxMobility = sonarBinToPrecursorMz.Value;
                    bin = (int)spectrumMinMobility;
                    sonarBinToPrecursorMz = _msDataFileScanHelper.ScanProvider.SonarBinToPrecursorMz(bin);
                    Assume.IsTrue(sonarBinToPrecursorMz.HasValue, @"error determining m/z value for SONAR bin #" + bin);
                    spectrumMinMobility = sonarBinToPrecursorMz.Value;
                }
                minIonMobility = Math.Min(minIonMobility, spectrumMinMobility);
                maxIonMobility = Math.Max(maxIonMobility, spectrumMaxMobility);
            }
        }

        private void ClearGraph()
        {
            comboBoxScanType.Items.Clear();
            comboBoxScanType.Enabled = false;
            lblScanId.Text = string.Empty;
            leftButton.Enabled = rightButton.Enabled = false;
            GraphPane.Title.Text = _msDataFileScanHelper.FileName;
        }

        [Browsable(true)]
        public event EventHandler<SelectedScanEventArgs> SelectedScanChanged;

        public void FireSelectedScanChanged(double retentionTime)
        {
            IsLoaded = true;
            var transitionId = _msDataFileScanHelper.CurrentTransition?.Id;
            SelectedScanChanged?.Invoke(this,
                _msDataFileScanHelper.MsDataSpectra != null && transitionId != null
                    ? new SelectedScanEventArgs(_msDataFileScanHelper.ScanProvider.DataFilePath, retentionTime, transitionId,
                        _msDataFileScanHelper.OptStep)
                    : new SelectedScanEventArgs(null, 0, null, null));
        }

        public bool IsLoaded { get; private set; }

        public void OnDocumentUIChanged(object sender, DocumentChangedEventArgs e)
        {
            if (ReferenceEquals(DocumentUI.Id, e.DocumentPrevious.Id) &&
                !ReferenceEquals(e.DocumentPrevious?.Settings.TransitionSettings.Libraries, DocumentUI.Settings.TransitionSettings.Libraries))
            {
                LoadScan(true, true);
                return;
            }
            // If document changed, reload scan.
            // Also reload if ion mobility is in use (as implied by visibility of related controls), as changes to
            // the IM library don't cause a document ID change (similar to spectral libraries, its contents exist outside of Skyline)
            if (e.DocumentPrevious == null || !ReferenceEquals(DocumentUI.Id, e.DocumentPrevious.Id) || spectrumBtn.Visible)
            {
                _msDataFileScanHelper.ScanProvider.SetScanProvider(null);

                LoadScan(true, true);
            }
        }

        private void GraphFullScan_VisibleChanged(object sender, EventArgs e)
        {
            if (IsHidden)
            {
                _msDataFileScanHelper.MsDataSpectra = null;
                FireSelectedScanChanged(0);
                UpdateUI(false);
            }
        }

        private void ZoomXAxis()
        {
            if (_msDataFileScanHelper.ScanProvider == null || _msDataFileScanHelper.ScanProvider.Transitions.Length == 0)
                return;

            ApplyXZoomToPane(GraphPane);
            if (IsDualPane)
                ApplyXZoomToPane(_stickSpectrumPane);
        }

        private void ApplyXZoomToPane(GraphPane pane)
        {
            var xScale = pane.XAxis.Scale;
            xScale.MinAuto = xScale.MaxAuto = false;

            if (magnifyBtn.Checked)
            {
                double mz = _msDataFileScanHelper.Source == ChromSource.ms1
                    ? _msDataFileScanHelper.ScanProvider.Transitions[_msDataFileScanHelper.TransitionIndex].PrecursorMz
                    : _msDataFileScanHelper.ScanProvider.Transitions[_msDataFileScanHelper.TransitionIndex].ProductMz;
                xScale.Min = mz - 1.5;
                xScale.Max = mz + 3.5;
            }
            else if (_requestedRange != null)
            {
                xScale.Min = _requestedRange.Min;
                xScale.Max = _requestedRange.Max;
            }
        }

        public void SetMzScale(MzRange range)
        {
            _requestedRange = range;
            if(magnifyBtn.Checked)
                magnifyBtn.Checked = false;
            else
                ZoomXAxis();
            _requestedRange = new MzRange(0, _maxMz * 1.1);
            using (var g = graphControl.CreateGraphics())
            {
                GraphPane.SetScale(g);
                if (IsDualPane)
                    _stickSpectrumPane.SetScale(g);
            }

            graphControl.Refresh();
        }

        public void SetIntensityScale(double maxIntensity)
        {
            var targetPane = IsDualPane ? _stickSpectrumPane : (GraphPane)_heatMapPane;
            targetPane.YAxis.Scale.MaxAuto = false;
            targetPane.YAxis.Scale.Max = maxIntensity;
            targetPane.AxisChange();
        }

        public MzRange Range
        {
            get { return new MzRange(GraphPane.XAxis.Scale.Min, GraphPane.XAxis.Scale.Max); }
        }

        public void ApplyMZZoomState(ZoomState newState)
        {
            newState.XAxis.ApplyScale(GraphPane.XAxis);
            if (IsDualPane)
                newState.XAxis.ApplyScale(_stickSpectrumPane.XAxis);
            using (var g = graphControl.CreateGraphics())
            {
                GraphPane.SetScale(g);
                if (IsDualPane)
                    _stickSpectrumPane.SetScale(g);
            }
            graphControl.Refresh();
        }

        public event EventHandler<ZoomEventArgs> ZoomEvent;

        private void graphControl_ZoomEvent(ZedGraphControl sender, ZoomState oldState, ZoomState newState, PointF mousePosition)
        {
            SyncMobilogramYAxisFromHeatmap();
            // Propagate X from whichever of stick/heatmap the user just zoomed in to the
            // other one. Using the pane under the mouse keeps wheel zooms over the stick
            // pane working without snapping back.
            if (IsDualPane)
            {
                var src = graphControl.MasterPane.FindPane(Point.Truncate(mousePosition));
                if (ReferenceEquals(src, _stickSpectrumPane))
                    CopyXAxis(_heatMapPane, _stickSpectrumPane);
                else if (ReferenceEquals(src, _heatMapPane))
                    CopyXAxis(_stickSpectrumPane, _heatMapPane);
            }
            FireZoomEvent(newState);
        }

        private static void CopyXAxis(GraphPane target, GraphPane source)
        {
            var dst = target.XAxis.Scale;
            var src = source.XAxis.Scale;
            dst.Min = src.Min;
            dst.Max = src.Max;
            dst.MinAuto = false;
            dst.MaxAuto = false;
        }

        private void graphControl_Resize(object sender, EventArgs e)
        {
            if (IsDualPane)
            {
                using (var g = graphControl.CreateGraphics())
                    graphControl.MasterPane.DoLayout(g);
                if (IsMobilogramPaneVisible)
                    AdjustFourPaneColumnWidths();
                else
                    AdjustTwoPaneRowHeights();
            }
            SyncMobilogramYAxisFromHeatmap();
        }

        /// <summary>
        /// Copy the heatmap's Y-axis (ion mobility) range to the mobilogram pane so
        /// rows in the two panes stay visually aligned.
        /// </summary>
        private void SyncMobilogramYAxisFromHeatmap()
        {
            if (!IsMobilogramPaneVisible)
                return;
            CopyYAxisFromHeatmap(_mobilogramPane, _heatMapPane);
        }

        private void FireZoomEvent(ZoomState zoomState = null)
        {
            if (ZoomEvent != null)
            {
                if (zoomState == null)
                    zoomState = new ZoomState(GraphPane, ZoomState.StateType.Zoom);
                ZoomEvent.Invoke(this, new ZoomEventArgs(zoomState));
            }
        }

        public SpectrumControlType ControlType
        {
            get { return SpectrumControlType.FullScanViewer; }
        }

        public bool IsAnnotated => _showIonSeriesAnnotations;
        public LibraryRankedSpectrumInfo SpectrumInfo => _rmis;

        public bool ShowPropertiesSheet
        {
            set
            {
                graphControlExtension.ShowPropertiesSheet(value);
                propertiesBtn.Checked = value;
            }
            get
            {
                return graphControlExtension.PropertiesVisible;
            }
        }

        public bool HasChromatogramData => false;

        private void ZoomYAxis()
        {
            if (_msDataFileScanHelper.ScanProvider == null || _msDataFileScanHelper.ScanProvider.Transitions.Length == 0)
                return;

            if (IsDualPane)
            {
                ZoomStickYAxis();
                ZoomHeatMapYAxis();
                return;
            }

            var yScale = GraphPane.YAxis.Scale;
            yScale.MinAuto = yScale.MaxAuto = false;
            bool isSpectrum = !spectrumBtn.Visible || spectrumBtn.Checked;
            GraphPane.LockYAxisMinAtZero = isSpectrum;

            // Auto scale graph for spectrum view.
            if (isSpectrum)
            {
                yScale.Min = 0;
                yScale.Max = _maxIntensity * 1.1;
                if (magnifyBtn.Checked)
                {
                    yScale.MaxAuto = true;
                }
            }
            else if (!filterBtn.Checked && !magnifyBtn.Checked)
            {
                var margin = 0.1 * (_maxIonMobility - _minIonMobility);
                yScale.Min = _minIonMobility - margin;
                yScale.Max = _maxIonMobility + margin;
            }
            else
            {
                double minDriftTime, maxDriftTime;
                bool hasIM = _msDataFileScanHelper.GetIonMobilityFilterDisplayRange(out minDriftTime, out maxDriftTime, _msDataFileScanHelper.Source);
                // hasIM may be false (e.g. when the originally clicked transition's source
                // doesn't match the currently selected scan type) and leave the out values
                // at MaxValue/MinValue, which would invert the Y axis. Require a valid range.
                if (hasIM && minDriftTime < maxDriftTime &&
                    minDriftTime > double.MinValue && maxDriftTime < double.MaxValue)
                {
                    double range = filterBtn.Checked
                        ? (maxDriftTime - minDriftTime)/2
                        : (maxDriftTime - minDriftTime)*2;
                    yScale.Min = minDriftTime - range;
                    yScale.Max = maxDriftTime + range;
                }
                else
                {
                    yScale.Min = 0;
                    yScale.Max = _maxIonMobility * 1.1;
                }
            }
            GraphPane.AxisChange();
        }

        private void ZoomStickYAxis()
        {
            if (_stickSpectrumPane == null) return;
            var yScale = _stickSpectrumPane.YAxis.Scale;
            yScale.MinAuto = yScale.MaxAuto = false;
            _stickSpectrumPane.LockYAxisMinAtZero = true;
            yScale.Min = 0;

            if (magnifyBtn.Checked)
            {
                // Compute max intensity from visible data in the current X range
                double xMin = _stickSpectrumPane.XAxis.Scale.Min;
                double xMax = _stickSpectrumPane.XAxis.Scale.Max;
                double maxY = 0;
                foreach (var curve in _stickSpectrumPane.CurveList)
                {
                    for (int i = 0; i < curve.Points.Count; i++)
                    {
                        var pt = curve.Points[i];
                        if (pt.X >= xMin && pt.X <= xMax && pt.Y > maxY)
                            maxY = pt.Y;
                    }
                }
                yScale.Max = maxY > 0 ? maxY * 1.1 : _maxIntensity * 1.1;
            }
            else
            {
                yScale.Max = _maxIntensity * 1.1;
            }
            _stickSpectrumPane.AxisChange();
        }

        private void ZoomHeatMapYAxis()
        {
            var yScale = _heatMapPane.YAxis.Scale;
            yScale.MinAuto = yScale.MaxAuto = false;
            _heatMapPane.LockYAxisMinAtZero = false;

            if (!filterBtn.Checked && !magnifyBtn.Checked)
            {
                var margin = 0.1 * (_maxIonMobility - _minIonMobility);
                yScale.Min = _minIonMobility - margin;
                yScale.Max = _maxIonMobility + margin;
            }
            else
            {
                double minDriftTime, maxDriftTime;
                _msDataFileScanHelper.GetIonMobilityFilterDisplayRange(out minDriftTime, out maxDriftTime, _msDataFileScanHelper.Source);
                if (minDriftTime > double.MinValue && maxDriftTime < double.MaxValue)
                {
                    double range = filterBtn.Checked
                        ? (maxDriftTime - minDriftTime) / 2
                        : (maxDriftTime - minDriftTime) * 2;
                    yScale.Min = minDriftTime - range;
                    yScale.Max = maxDriftTime + range;
                }
                else
                {
                    yScale.Min = 0;
                    yScale.Max = _maxIonMobility * 1.1;
                }
            }
            _heatMapPane.AxisChange();
        }

        public void UpdateUI(bool selectionChanged = true)
        {
            // Only worry about updates, if the graph is visible
            // And make sure it is not disposed, since rendering happens on a timer
            if (!Visible || IsDisposed || _msDataFileScanHelper.ScanProvider == null)
                return;
            GraphHelper.FormatGraphPane(GraphPane);
            if (IsDualPane)
                GraphHelper.FormatGraphPane(_stickSpectrumPane);
            comboBoxPeakType.Visible = IsDualPane || spectrumBtn.Checked;
            toolStripLabelPeakType.Visible = IsDualPane || spectrumBtn.Checked;

            if (selectionChanged)
                CreateGraph();

            if (_msDataFileScanHelper.MsDataSpectra != null)
            {
                leftButton.Enabled = (_msDataFileScanHelper.ScanIndex > 0);
                rightButton.Enabled = (_msDataFileScanHelper.ScanIndex < _msDataFileScanHelper.ScanProvider.Times.Count-1);
                lblScanId.Text = _msDataFileScanHelper.GetScanIndex().ToString(@"D");
                GraphPane.SetScale(CreateGraphics());
                if (IsDualPane)
                    _stickSpectrumPane.SetScale(CreateGraphics());
                if (_msDataFileScanHelper.IsWatersSonarData)
                {
                    filterBtn.ToolTipText = GraphsResources.GraphFullScan_Filter_Button_Tooltip_Filter_Quadrupole_Scan_Range;
                }
            }
            else
            {
                leftButton.Enabled = rightButton.Enabled = false;
                lblScanId.Text = string.Empty;
            }

            graphControl.Refresh();
        }

        public void LockYAxis(bool lockY)
        {
            graphControl.IsEnableVPan = graphControl.IsEnableVZoom = !lockY;
            graphControl.Refresh();
        }

        protected override void OnClosed(EventArgs e)
        {
            _cursorTip.Dispose();
            graphControlExtension.PropertiesSheet.SelectedObject = null;
            _documentContainer.UnlistenUI(OnDocumentUIChanged);
            _msDataFileScanHelper.Dispose();
            base.OnClosed(e);
        }

        private void leftButton_Click(object sender, EventArgs e)
        {
            ChangeScan(-1);
        }

        private void rightButton_Click(object sender, EventArgs e)
        {
            ChangeScan(1);
        }

        public void ChangeScan(int delta)
        {
            if (_msDataFileScanHelper.MsDataSpectra == null)
                return;
            if (_msDataFileScanHelper.ScanIndex + delta < 0 || _msDataFileScanHelper.ScanIndex + delta >= _msDataFileScanHelper.ScanProvider.Times.Count)
                return;

            var sourceScanIds = _msDataFileScanHelper.TimeIntensities.ScanIds;
            int scanId = sourceScanIds[_msDataFileScanHelper.ScanIndex];
            while ((delta < 0 && _msDataFileScanHelper.ScanIndex > 0) || (delta > 0 && _msDataFileScanHelper.ScanIndex < sourceScanIds.Count-1))
            {
                _msDataFileScanHelper.ScanIndex += delta;
                int newScanId = sourceScanIds[_msDataFileScanHelper.ScanIndex];
                if (newScanId != scanId)
                {
                    if (delta < 0)
                    {
                        // Choose first scan with a particular scanId by continuing backward until another
                        // change in scan ID is found.
                        while (_msDataFileScanHelper.ScanIndex > 0 && sourceScanIds[_msDataFileScanHelper.ScanIndex - 1] == sourceScanIds[_msDataFileScanHelper.ScanIndex])
                            _msDataFileScanHelper.ScanIndex--;
                    }
                    break;
                }
            }

            LoadScan(false, Settings.Default.LockYAxis);
        }

        private void comboBoxScanType_SelectedIndexChanged(object sender, EventArgs e)
        {
            _msDataFileScanHelper.Source = _msDataFileScanHelper.SourceFromName(comboBoxScanType.Text);
            LoadScan(true, true);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.Escape:
                    _documentContainer.FocusDocument();
                    return true;
                case Keys.Left:
                    ChangeScan(-1);
                    return true;
                case Keys.Right:
                    ChangeScan(1);
                    return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void magnifyBtn_CheckedChanged(object sender, EventArgs e)
        {
            ZoomToSelection(magnifyBtn.Checked);
        }

        public void ZoomToSelection(bool zoom)
        {
            Settings.Default.AutoZoomFullScanGraph = magnifyBtn.Checked = zoom;
            ZoomXAxis();
            ZoomYAxis();
            UpdateUI();
            FireZoomEvent();
        }

        private void spectrumBtn_CheckedChanged(object sender, EventArgs e)
        {
            ShowMobility(!spectrumBtn.Checked);
        }

        public void ShowMobility(bool show)
        {
            HeatMapGraphPane.ShowHeatMap = show;
            Settings.Default.SumScansFullScan = spectrumBtn.Checked = !show;
            mobilogramBtn.Visible = show && spectrumBtn.Visible;
            UpdateUI();
            ZoomYAxis();
            graphControl.Invalidate();
        }

        private HeatMapGraphPane HeatMapGraphPane { get { return _heatMapPane; } }

        private struct ExtractionBoxInfo
        {
            public readonly double CenterMz;
            public readonly double OriginalWidth;

            public ExtractionBoxInfo(double centerMz, double originalWidth)
            {
                CenterMz = centerMz;
                OriginalWidth = originalWidth;
            }
        }

        /// <summary>
        /// MSGraphPane subclass that enforces minimum extraction box width during rendering,
        /// matching the behavior of FullScanHeatMapGraphPane.
        /// </summary>
        private class FullScanStickSpectrumPane : MSGraphPane
        {
            public FullScanStickSpectrumPane(LabelBoundsCache cache) : base(cache) { }

            public override void SetScale(Graphics g)
            {
                base.SetScale(g);
                FullScanHeatMapGraphPane.EnforceMinExtractionBoxWidth(this);
            }
        }

        /// <summary>
        /// Extends <see cref="HeatMapGraphPane"/> with <see cref="IHeatMapDataProvider"/> so that
        /// Copy Data produces clean 3-column output instead of per-curve data.
        /// </summary>
        private class FullScanHeatMapGraphPane : HeatMapGraphPane, IHeatMapDataProvider
        {
            public HeatMapData HeatMapData => ShowHeatMap ? _heatMapData : null;
            public string HeatMapZAxisName => GraphsResources.AbstractMSGraphItem_CustomizeYAxis_Intensity;

            public override void SetScale(Graphics g)
            {
                base.SetScale(g);
                EnforceMinExtractionBoxWidth(this);
            }

            /// <summary>
            /// Enforce a minimum rendered width of 1 pixel for extraction boxes so they
            /// remain visible when the m/z axis is zoomed out to a wide range.
            /// Can be applied to any GraphPane that has ExtractionBoxInfo-tagged BoxObjs.
            /// </summary>
            public static void EnforceMinExtractionBoxWidth(GraphPane pane)
            {
                // AxisChange (called by base.SetScale) updates axis Min/Max but does not
                // call SetupScaleData, so the pixel-to-data transform used by
                // ReverseTransform can be stale from a previous zoom level.
                pane.XAxis.Scale.SetupScaleData(pane, pane.XAxis);
                double minWidth = Math.Abs(pane.XAxis.Scale.ReverseTransform(1) - pane.XAxis.Scale.ReverseTransform(0));
                if (double.IsNaN(minWidth) || double.IsInfinity(minWidth) || minWidth <= 0)
                    return;
                foreach (var obj in pane.GraphObjList.OfType<BoxObj>())
                {
                    if (!(obj.Tag is ExtractionBoxInfo info))
                        continue;
                    double w = Math.Max(info.OriginalWidth, minWidth);
                    obj.Location.X = info.CenterMz - w / 2;
                    obj.Location.Width = w;
                }
            }
        }

        private void filterBtn_CheckedChanged(object sender, EventArgs e)
        {
            FilterDriftTimes(filterBtn.Checked);
        }

        public void FilterDriftTimes(bool filter)
        {
            Settings.Default.FilterIonMobilityFullScan = filterBtn.Checked = filter;
            _zoomYAxis = true;
            SetSpectraUI(_msDataFileScanHelper.MsDataSpectra);
        }

        private void mobilogramBtn_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.ShowMobilogramFullScan = mobilogramBtn.Checked;
            _heatMapData = null; // Force recompute with/without PlotY2D
            // Preserve current axis ranges across the rebuild
            double savedYMin = _heatMapPane.YAxis.Scale.Min;
            double savedYMax = _heatMapPane.YAxis.Scale.Max;
            UpdateUI();
            // Restore heatmap Y range (mobilogram toggle shouldn't change the view)
            if (IsDualPane)
            {
                _heatMapPane.YAxis.Scale.Min = savedYMin;
                _heatMapPane.YAxis.Scale.Max = savedYMax;
                graphControl.Invalidate();
            }
        }

        private void CreateMobilogram()
        {
            if (_heatMapData?.PlotY2D == null || _mobilogramPane == null)
                return;

            // Use the same filter display range as the heatmap for the band
            double filterMin = double.NaN, filterMax = double.NaN, filterPeak = double.NaN;
            double minDrift, maxDrift;
            _msDataFileScanHelper.GetIonMobilityFilterDisplayRange(
                out minDrift, out maxDrift, _msDataFileScanHelper.Source);
            if (minDrift > 0 && maxDrift < double.MaxValue)
            {
                filterMin = minDrift;
                filterMax = maxDrift;
            }

            // Peak center is only meaningful when the filter band is shown
            if (!double.IsNaN(filterMin))
            {
                if (_msDataFileScanHelper.IsWatersSonarData)
                {
                    // For SONAR, the Y-axis is precursor m/z
                    var currentTransition = _msDataFileScanHelper.CurrentTransition;
                    if (currentTransition != null)
                        filterPeak = currentTransition.PrecursorMz;
                }
                else
                {
                    var imFilter = _msDataFileScanHelper.CurrentTransition?.IonMobilityInfo;
                    if (imFilter != null && imFilter.HasIonMobilityValue)
                        filterPeak = imFilter.IonMobility.Mobility.Value +
                                     (imFilter.IonMobilityFilterWindow.Offset);
                }
            }

            // Compute per-transition Y projections for colored curves
            var transitionCurves = new List<MobilogramCurveSpec>();
            var transitions = _msDataFileScanHelper.ScanProvider.Transitions;
            if (transitions.Length > 0)
            {
                // Build a dictionary of IM -> summed intensity for each transition
                var perTransition = new Dictionary<int, Dictionary<float, double>>();
                for (int t = 0; t < transitions.Length; t++)
                {
                    if (transitions[t].Source != _msDataFileScanHelper.Source)
                        continue;
                    perTransition[t] = new Dictionary<float, double>();
                }

                if (perTransition.Count > 0)
                {
                    foreach (var pt in _heatMapData.GetAllPoints())
                    {
                        double mz = pt.Point.X;
                        float im = (float)pt.Point.Y;
                        double intensity = pt.Point.Z;
                        foreach (var kvp in perTransition)
                        {
                            if (transitions[kvp.Key].MatchMz(mz))
                            {
                                if (kvp.Value.ContainsKey(im))
                                    kvp.Value[im] += intensity;
                                else
                                    kvp.Value[im] = intensity;
                            }
                        }
                    }

                    // Build sorted IM grid from summed curve for gap detection
                    var imGrid = _heatMapData.PlotY2D.Select(p => p.Key).ToList(); // already sorted

                    foreach (var kvp in perTransition)
                    {
                        if (kvp.Value.Count > 0)
                        {
                            // Build curve with zero-bookends at gaps
                            var sorted = kvp.Value.OrderBy(p => p.Key).ToList();
                            var curve = new List<KeyValuePair<float, double>>();
                            for (int j = 0; j < sorted.Count; j++)
                            {
                                float im = sorted[j].Key;
                                int gridIdx = imGrid.BinarySearch(im);
                                if (gridIdx < 0) gridIdx = ~gridIdx;

                                bool isGap = j == 0 || (gridIdx > 0 &&
                                    !kvp.Value.ContainsKey(imGrid[gridIdx - 1]));
                                if (isGap && gridIdx > 0)
                                    curve.Add(new KeyValuePair<float, double>(imGrid[gridIdx - 1], 0));

                                curve.Add(new KeyValuePair<float, double>(im, sorted[j].Value));

                                bool isEndGap = j == sorted.Count - 1 || (gridIdx < imGrid.Count - 1 &&
                                    !kvp.Value.ContainsKey(imGrid[gridIdx + 1]));
                                if (isEndGap && gridIdx < imGrid.Count - 1)
                                    curve.Add(new KeyValuePair<float, double>(imGrid[gridIdx + 1], 0));
                            }
                            transitionCurves.Add(new MobilogramCurveSpec(
                                transitions[kvp.Key].Name, GetTransitionColor(transitions[kvp.Key]), curve));
                        }
                    }
                }
            }

            PopulateMobilogramPane(transitionCurves, filterMin, filterMax, filterPeak);
        }

        private readonly struct MobilogramCurveSpec
        {
            public readonly string Name;
            public readonly Color Color;
            public readonly List<KeyValuePair<float, double>> Data;
            public MobilogramCurveSpec(string name, Color color, List<KeyValuePair<float, double>> data)
            {
                Name = name;
                Color = color;
                Data = data;
            }
        }

        private void PopulateMobilogramPane(List<MobilogramCurveSpec> transitionCurves,
            double filterMin, double filterMax, double filterPeak)
        {
            if (_mobilogramPane == null)
                return;
            _mobilogramPane.CurveList.Clear();
            _mobilogramPane.GraphObjList.Clear();

            // Sync Y-axis to heatmap before computing anything
            SyncMobilogramYAxisFromHeatmap();

            // Compute max intensity for X-axis
            double maxIntensity = 0;
            if (transitionCurves.Count > 0)
            {
                foreach (var tc in transitionCurves)
                    foreach (var kvp in tc.Data)
                        if (kvp.Value > maxIntensity)
                            maxIntensity = kvp.Value;
            }
            else
            {
                foreach (var kvp in _heatMapData.PlotY2D)
                    if (kvp.Value > maxIntensity)
                        maxIntensity = kvp.Value;
            }
            if (maxIntensity <= 0)
                maxIntensity = 1;

            var xScale = _mobilogramPane.XAxis.Scale;
            xScale.Min = 0;
            xScale.Max = maxIntensity * 1.05;

            int lineWidth = Settings.Default.ChromatogramLineWidth;
            if (transitionCurves.Count > 0)
            {
                foreach (var tc in transitionCurves)
                {
                    var xs = new double[tc.Data.Count];
                    var ys = new double[tc.Data.Count];
                    for (int i = 0; i < tc.Data.Count; i++)
                    {
                        xs[i] = tc.Data[i].Value;   // intensity on X
                        ys[i] = tc.Data[i].Key;     // ion mobility on Y
                    }
                    var line = new LineItem(tc.Name ?? string.Empty, xs, ys, tc.Color, SymbolType.None, lineWidth);
                    line.Line.IsAntiAlias = true;
                    line.Symbol.IsVisible = false;
                    _mobilogramPane.CurveList.Add(line);
                }
            }
            else
            {
                var data = _heatMapData.PlotY2D;
                var xs = new double[data.Count];
                var ys = new double[data.Count];
                for (int i = 0; i < data.Count; i++)
                {
                    xs[i] = data[i].Value;
                    ys[i] = data[i].Key;
                }
                var line = new LineItem(string.Empty, xs, ys, Color.DarkBlue, SymbolType.None, lineWidth);
                line.Line.IsAntiAlias = true;
                line.Line.Fill = new Fill(Color.FromArgb(40, Color.DarkBlue));
                line.Symbol.IsVisible = false;
                _mobilogramPane.CurveList.Add(line);
            }

            // Filter band as a BoxObj spanning full X range between filterMin and filterMax
            if (!double.IsNaN(filterMin) && !double.IsNaN(filterMax))
            {
                var band = new BoxObj(xScale.Min, filterMax, xScale.Max - xScale.Min, filterMax - filterMin,
                    Color.FromArgb(100, Color.DarkViolet), Color.FromArgb(50, Color.Gray))
                {
                    IsClippedToChartRect = true,
                    ZOrder = ZOrder.E_BehindCurves,
                };
                band.Border.Width = 2;
                _mobilogramPane.GraphObjList.Add(band);
            }

            // Peak center as a dashed horizontal line
            if (!double.IsNaN(filterPeak))
            {
                var peakLine = new LineObj(Color.FromArgb(180, Color.DarkViolet),
                    xScale.Min, filterPeak, xScale.Max, filterPeak)
                {
                    IsClippedToChartRect = true,
                    ZOrder = ZOrder.A_InFront,
                };
                peakLine.Line.Width = 1.5f;
                peakLine.Line.Style = System.Drawing.Drawing2D.DashStyle.Dash;
                _mobilogramPane.GraphObjList.Add(peakLine);
            }

            using (var g = graphControl.CreateGraphics())
                _mobilogramPane.AxisChange(g);
            // AxisChange may recompute Y from data — resync to match heatmap Y range
            SyncMobilogramYAxisFromHeatmap();
            AlignMobilogramChartToHeatmap();
        }


        // CONSIDER: This button is never visible and appears to be completely idle. Remove?
        private void btnIsolationWindow_Click(object sender, EventArgs e)
        {
            var spectrum = _msDataFileScanHelper.MsDataSpectra[0];
            var target = spectrum.Precursors[0].IsolationWindowTargetMz;
            if (!target.HasValue)
                MessageDlg.Show(this, @"No isolation target");
            else
            {
                double low = target.Value - spectrum.Precursors[0].IsolationWindowLower ?? SignedMz.ZERO;
                double high = target.Value + spectrum.Precursors[0].IsolationWindowUpper ?? SignedMz.ZERO;
                MessageDlg.Show(this, string.Format(@"Isolation window: {0}, {1}, {2}", low, target, high));
            }
        }


        private void propertiesBtn_Click(object sender, EventArgs e)
        {
            ShowPropertiesSheet = !ShowPropertiesSheet;
        }

        public MenuControl<T> GetHostedControl<T>() where T:Panel, IControlSize, new()
        {
                if (ZedGraphControl.ContextMenuStrip != null)
                {
                    var chargesItem = ZedGraphControl.ContextMenuStrip.Items.OfType<ToolStripMenuItem>()
                        .FirstOrDefault(item => item.DropDownItems.OfType<MenuControl<T>>().Any());
                    if (chargesItem != null)
                        return chargesItem.DropDownItems[0] as MenuControl<T>;
                }
                return null;
        }

        public void DisconnectHandlers()
        {
            if (_documentContainer is SkylineWindow skylineWindow)
            {
                var chargeSelector = GetHostedControl<ChargeSelectionPanel>();

                if (chargeSelector != null)
                {
                    chargeSelector.HostedControl.OnChargeChanged -= skylineWindow.IonChargeSelector_ionChargeChanged;
                }

                var ionTypeSelector = GetHostedControl<IonTypeSelectionPanel>();
                if (ionTypeSelector != null)
                {
                    ionTypeSelector.HostedControl.IonTypeChanged -= skylineWindow.IonTypeSelector_IonTypeChanges;
                    ionTypeSelector.HostedControl.LossChanged -= skylineWindow.IonTypeSelector_LossChanged;
                }
            }
        }

        #region Mouse events

        private void graphControl_ContextMenuBuilder(ZedGraphControl sender, ContextMenuStrip menuStrip, Point mousePt, ZedGraphControl.ContextMenuObjectState objState)
        {
            if (_msDataFileScanHelper.MsDataSpectra != null)
            {
                showPeakAnnotationsContextMenuItem.Checked = Settings.Default.ShowFullScanAnnotations = _showIonSeriesAnnotations;
                menuStrip.Items.Insert(0, showPeakAnnotationsContextMenuItem);
                menuStrip.Items.Insert(1, toolStripSeparator1);

                var isProteomic = (_msDataFileScanHelper.CurrentTransition?.Id as Transition)?.Group.IsProteomic;
                (_documentContainer as GraphSpectrum.IStateProvider)
                    ?.BuildSpectrumMenu(isProteomic.GetValueOrDefault(), sender, menuStrip);
            }
        }

        private void graphControl_MouseClick(object sender, MouseEventArgs e)
        {
            MSGraphPane labelPane = IsDualPane ? _stickSpectrumPane : GraphPane;
            var nearestLabel = GetNearestLabel(new PointF(e.X, e.Y), labelPane);
            if (nearestLabel == null)
                return;
            //if the spectrum is annotated with ion series the label tags have peak rank, not transition index
            //translating rank into index here to make sure the label click event works correctly
            var transitionIndex = -1;
            if (_showIonSeriesAnnotations)
                transitionIndex = _transitionIndex[(int)nearestLabel.Tag];
            else
                transitionIndex = (int) nearestLabel.Tag;

            if (transitionIndex >= 0)
            {
                _msDataFileScanHelper.TransitionIndex = transitionIndex;
                magnifyBtn.Checked = true;
                UpdateUI();
                ZoomXAxis();
                ZoomYAxis();
            }
        }

        private bool graphControl_MouseMove(ZedGraphControl sender, MouseEventArgs e)
        {
            var pt = new PointF(e.X, e.Y);

            // In the mobilogram area, show the same crosshair cursor as the heatmap.
            // ZedGraph only sets Cross when FindChartRect returns non-null, but the mobilogram
            // lives in the right margin outside the chart rect, so we must set it explicitly.
            if (IsMobilogramVisible && IsInMobilogramArea(pt))
            {
                graphControl.Cursor = Cursors.Cross;
                return true;
            }

            // Labels only exist on the stick pane
            MSGraphPane labelPane = IsDualPane ? _stickSpectrumPane : GraphPane;
            if (IsDualPane)
            {
                var pane = graphControl.MasterPane.FindChartRect(pt);
                if (!ReferenceEquals(pane, _stickSpectrumPane))
                    return false;
            }

            var nearestLabel = GetNearestLabel(pt, labelPane);
            if (nearestLabel == null || nearestLabel.Tag == null)
                return false;
            var transition = (int) nearestLabel.Tag;
            if (transition < 0 || _transitionIndex == null || transition >= _transitionIndex.Length)
                return false;
            if (_showIonSeriesAnnotations && _transitionIndex[(int)nearestLabel.Tag] < 0)
                return false;

            graphControl.Cursor = Cursors.Hand;
            return true;
        }

        // For use with CursorTrackingTip _cursorTip
        private TableDesc GetTooltipTable(PointF pt)
        {
            // Only show tooltip when the cursor is the crosshair — this naturally suppresses
            // tooltips near (but outside) the chart area, e.g. near the legend.
            if (graphControl.Cursor != Cursors.Cross)
                return null;
            if (IsMobilogramVisible && IsInMobilogramArea(pt))
                return GetMobilogramTooltipTable(pt);

            if (IsDualPane)
            {
                var pane = graphControl.MasterPane.FindChartRect(pt);
                if (ReferenceEquals(pane, _stickSpectrumPane))
                    return GetSpectrumTooltipTable(pt, _stickSpectrumPane);
                if (ReferenceEquals(pane, _heatMapPane))
                    return GetHeatMapTooltipTable(pt);
                return null;
            }

            bool isHeatMap = spectrumBtn.Visible && !spectrumBtn.Checked;
            return isHeatMap ? GetHeatMapTooltipTable(pt) : GetSpectrumTooltipTable(pt);
        }

        private bool IsInMobilogramArea(PointF pt)
        {
            if (!IsMobilogramPaneVisible)
                return false;
            var chartRect = _mobilogramPane.Chart.Rect;
            return chartRect.Contains(pt);
        }

        private TableDesc GetMobilogramTooltipTable(PointF pt)
        {
            if (_mobilogramPane == null || _mobilogramPane.CurveList.Count == 0)
                return null;

            // Find the curve whose nearest LINE SEGMENT (not just vertex) passes within
            // a few pixels of the cursor. Tooltip itself reports the value at the nearer
            // of the segment's two endpoints — no interpolation.
            float scaleFactor = _mobilogramPane.CalcScaleFactor();
            float maxDistPx = 10f * scaleFactor;
            float bestDistPx = maxDistPx;
            CurveItem bestCurve = null;
            int bestEndpointIdx = -1;
            foreach (var curve in _mobilogramPane.CurveList)
            {
                var pts = curve.Points;
                if (pts == null || pts.Count < 2) continue;
                float prevPxX = _mobilogramPane.XAxis.Scale.Transform(pts[0].X);
                float prevPxY = _mobilogramPane.YAxis.Scale.Transform(pts[0].Y);
                for (int i = 1; i < pts.Count; i++)
                {
                    float curPxX = _mobilogramPane.XAxis.Scale.Transform(pts[i].X);
                    float curPxY = _mobilogramPane.YAxis.Scale.Transform(pts[i].Y);
                    float dist = DistancePointToSegment(pt.X, pt.Y, prevPxX, prevPxY, curPxX, curPxY);
                    if (dist < bestDistPx)
                    {
                        bestDistPx = dist;
                        bestCurve = curve;
                        // Report the closer of the two endpoints
                        float dPrev = (pt.X - prevPxX) * (pt.X - prevPxX) + (pt.Y - prevPxY) * (pt.Y - prevPxY);
                        float dCur  = (pt.X - curPxX)  * (pt.X - curPxX)  + (pt.Y - curPxY)  * (pt.Y - curPxY);
                        bestEndpointIdx = dPrev <= dCur ? i - 1 : i;
                    }
                    prevPxX = curPxX;
                    prevPxY = curPxY;
                }
            }
            if (bestCurve == null) return null;

            var endpointX = bestCurve.Points[bestEndpointIdx].X;
            var endpointY = bestCurve.Points[bestEndpointIdx].Y;

            var rt = _cursorTip.RenderTools;
            string yAxisLabel = _heatMapPane.YAxis.Title.Text ?? string.Empty;
            var table = new TableDesc();
            table.AddDetailRow(yAxisLabel, endpointY.ToString(Formats.IonMobility), rt);
            if (!string.IsNullOrEmpty(bestCurve.Label?.Text))
                table.AddDetailRow(GraphsResources.GraphFullScan_ToolTip_Transition, bestCurve.Label.Text, rt);
            table.AddDetailRow(GraphsResources.GraphFullScan_ToolTip_Summed_Intensity, endpointX.ToString(@"F0"), rt);
            return table;
        }

        private static float DistancePointToSegment(float px, float py, float ax, float ay, float bx, float by)
        {
            float dx = bx - ax, dy = by - ay;
            float len2 = dx * dx + dy * dy;
            if (len2 <= float.Epsilon)
            {
                float ex = px - ax, ey = py - ay;
                return (float)Math.Sqrt(ex * ex + ey * ey);
            }
            float t = ((px - ax) * dx + (py - ay) * dy) / len2;
            if (t < 0) t = 0; else if (t > 1) t = 1;
            float qx = ax + t * dx, qy = ay + t * dy;
            float ddx = px - qx, ddy = py - qy;
            return (float)Math.Sqrt(ddx * ddx + ddy * ddy);
        }

        private TableDesc GetHeatMapTooltipTable(PointF pt)
        {
            if (_heatMapData == null)
                return null;

            double x, y;
            GraphPane.ReverseTransform(pt, out x, out y);

            // Search radius in data coordinates: use a small pixel neighborhood
            const float searchPixels = 10f;
            double xLo, xHi, yLo, yHi;
            GraphPane.ReverseTransform(new PointF(pt.X - searchPixels, pt.Y - searchPixels), out xLo, out yHi);
            GraphPane.ReverseTransform(new PointF(pt.X + searchPixels, pt.Y + searchPixels), out xHi, out yLo);

            double searchRadiusX = Math.Abs(xHi - xLo) / 2;
            double searchRadiusY = Math.Abs(yHi - yLo) / 2;

            // Use a small cell size to get individual points from the quad-tree
            var candidates = _heatMapData.GetPoints(
                x - searchRadiusX, x + searchRadiusX,
                y - searchRadiusY, y + searchRadiusY,
                searchRadiusX / 2, searchRadiusY / 2);

            var nearest = FindNearestHeatMapPoint(candidates, x, y, searchRadiusX, searchRadiusY);
            if (nearest == null)
                return null;

            var rt = _cursorTip.RenderTools;
            string yAxisLabel = GraphPane.YAxis.Title.Text ?? string.Empty;
            var table = new TableDesc();
            table.AddDetailRow(GraphsResources.GraphFullScan_ToolTip_mz, nearest.Point.X.ToString(Formats.Mz), rt);
            table.AddDetailRow(yAxisLabel, nearest.Point.Y.ToString(Formats.IonMobility), rt);
            table.AddDetailRow(GraphsResources.GraphFullScan_ToolTip_Intensity, nearest.Point.Z.ToString(@"F0"), rt);
            return table;
        }

        private TableDesc GetSpectrumTooltipTable(PointF pt, MSGraphPane spectrumPane = null)
        {
            spectrumPane = spectrumPane ?? GraphPane;
            StickItem nearestCurve;
            int nearestIndex;
            double mz, intensity;

            if (spectrumPane.FindNearestStick(pt, out nearestCurve, out nearestIndex))
            {
                mz = nearestCurve[nearestIndex].X;
                intensity = nearestCurve[nearestIndex].Y;
            }
            else
            {
                // Cursor may be over an annotation label rather than the stick body itself.
                // The label's X is the predicted m/z; find the closest observed stick nearby.
                var label = GetNearestLabel(pt, spectrumPane);
                if (label == null)
                    return null;
                var stickPoint = FindStickPointNearMz(label.Location.X, spectrumPane);
                if (stickPoint == null)
                    return null;
                mz = stickPoint.X;
                intensity = stickPoint.Y;
            }

            var rt = _cursorTip.RenderTools;
            var table = new TableDesc();
            table.AddDetailRow(GraphsResources.GraphFullScan_ToolTip_mz, mz.ToString(Formats.Mz), rt);
            table.AddDetailRow(GraphsResources.GraphFullScan_ToolTip_Intensity, intensity.ToString(@"F0"), rt);
            // Look up matched ion info; used both to suppress the redundant Transition row and to build the table.
            LibraryRankedSpectrumInfo.RankedMI rmi = null;
            if (_rmis != null)
                rmi = _rmis.Peaks.FirstOrDefault(p => p.ObservedMz == mz);
            bool hasMatchedIons = rmi != null && rmi.MatchedIons != null && rmi.MatchedIons.Count > 0;

            // In non-annotation mode, show the transition name when the peak falls within a transition's
            // extraction window — but skip it when the Matched Ions section already names the same ion.
            if (!_showIonSeriesAnnotations && !hasMatchedIons && _msDataFileScanHelper.ScanProvider != null)
            {
                var matchedTransition = _msDataFileScanHelper.ScanProvider.Transitions.FirstOrDefault(
                    t => t.Source == _msDataFileScanHelper.Source && t.MatchMz(mz));
                if (matchedTransition != null)
                    table.AddDetailRow(GraphsResources.GraphFullScan_ToolTip_Transition, matchedTransition.Name, rt);
            }
            // Show matched ion info whenever available (explains ion-series coloring in annotation mode,
            // and provides predicted m/z and mass error context in non-annotation mode too).
            if (hasMatchedIons)
            {
                table.AddDetailRowNoBold(@"  ", @"  ", rt); // blank separator line
                table.AddDetailRow(GraphsResources.GraphSpectrum_ToolTip_MatchedIons,
                    GraphsResources.ToolTipImplementation_RenderTip_Calculated_Mass, rt, true);
                foreach (var mfi in rmi.MatchedIons)
                    table.AddDetailRowNoBold(AbstractSpectrumGraphItem.GetLabel(mfi, rmi.Rank, false, !_showIonSeriesAnnotations),
                        mfi.PredictedMz.ToString(Formats.Mz, CultureInfo.CurrentCulture) + @"  " +
                        AbstractSpectrumGraphItem.GetMassErrorString(rmi, mfi), rt);
            }
            return table;
        }

        private static HeatMapData.TaggedPoint3D FindNearestHeatMapPoint(
            List<HeatMapData.TaggedPoint3D> candidates,
            double x, double y, double searchRadiusX, double searchRadiusY)
        {
            if (candidates == null || candidates.Count == 0)
                return null;

            HeatMapData.TaggedPoint3D nearest = null;
            double minDist = double.MaxValue;
            foreach (var candidate in candidates)
            {
                // Normalize by search radius so m/z and ion mobility scales are comparable
                double dx = (candidate.Point.X - x) / searchRadiusX;
                double dy = (candidate.Point.Y - y) / searchRadiusY;
                double dist = dx * dx + dy * dy;
                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = candidate;
                }
            }

            return nearest;
        }

        /// <summary>
        /// Finds the closest StickItem point to the given m/z, within a 1 Da tolerance.
        /// Used to resolve the observed m/z and intensity from a label's predicted m/z.
        /// </summary>
        private PointPair FindStickPointNearMz(double mz, MSGraphPane spectrumPane = null)
        {
            spectrumPane = spectrumPane ?? GraphPane;
            PointPair best = null;
            double bestDist = double.MaxValue;
            foreach (var curve in spectrumPane.CurveList)
            {
                var stick = curve as StickItem;
                if (stick == null)
                    continue;
                for (int i = 0; i < stick.Points.Count; i++)
                {
                    double dist = Math.Abs(stick[i].X - mz);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        best = stick[i];
                    }
                }
            }
            return bestDist <= 1.0 ? best : null;
        }

        private TextObj GetNearestLabel(PointF mousePoint, MSGraphPane spectrumPane = null)
        {
            spectrumPane = spectrumPane ?? GraphPane;
            using (Graphics g = CreateGraphics())
            {
                object nearestObject;
                if (spectrumPane.FindNearestObject(mousePoint, g, out nearestObject, out _))
                {
                    var textObj = nearestObject as TextObj;
                    if (textObj != null)
                        return textObj;
                }
            }

            return null;
        }

        #endregion

        #region Test support

        public void TestMouseClick(double x, double y)
        {
            // In dual-pane mode, transform against stick pane for label clicks
            var pane = IsDualPane ? _stickSpectrumPane : GraphPane;
            var mouse = pane.GeneralTransform(new PointF((float)x, (float)y), CoordType.AxisXYScale);
            graphControl_MouseClick(null, new MouseEventArgs(MouseButtons.Left, 1, (int)mouse.X, (int)mouse.Y, 0));
        }

        public PointF TransformCoordinates(double x, double y, CoordType coordType = CoordType.AxisXYScale)
        {
            return GraphPane.GeneralTransform(new PointF((float)x, (float)y), coordType);
        }

        /// <summary>
        /// Returns the tooltip text for a data point nearest to the given coordinates,
        /// for testing. Bypasses pixel-based search (FindNearestPoint) which can fail
        /// in offscreen mode where the graph is too small for the 7-pixel threshold.
        /// For spectrum mode, x is m/z (y is ignored). For heatmap, x is m/z and y is
        /// ion mobility. Pass no arguments to use a representative midpoint.
        /// </summary>
        public TableDesc TestGetTooltipTable(double x = double.NaN, double y = double.NaN)
        {
            // Heatmap tooltip when heatmap is showing (dual-pane or single-pane heatmap mode)
            bool isHeatMap = IsDualPane || (spectrumBtn.Visible && !spectrumBtn.Checked);
            var rt = _cursorTip.RenderTools;
            var table = new TableDesc();

            if (isHeatMap)
            {
                if (_heatMapData == null)
                    return null;
                double searchX = double.IsNaN(x)
                    ? (GraphPane.XAxis.Scale.Min + GraphPane.XAxis.Scale.Max) / 2 : x;
                double searchY = double.IsNaN(y)
                    ? (GraphPane.YAxis.Scale.Min + GraphPane.YAxis.Scale.Max) / 2 : y;
                // Search a generous range around the target point
                double rangeX = (GraphPane.XAxis.Scale.Max - GraphPane.XAxis.Scale.Min) / 2;
                double rangeY = (GraphPane.YAxis.Scale.Max - GraphPane.YAxis.Scale.Min) / 2;
                double searchRadiusX = double.IsNaN(x) ? rangeX : rangeX / 10;
                double searchRadiusY = double.IsNaN(y) ? rangeY : rangeY / 10;
                var candidates = _heatMapData.GetPoints(
                    searchX - searchRadiusX, searchX + searchRadiusX,
                    searchY - searchRadiusY, searchY + searchRadiusY,
                    searchRadiusX / 4, searchRadiusY / 4);
                var nearest = FindNearestHeatMapPoint(candidates, searchX, searchY,
                    searchRadiusX, searchRadiusY);
                if (nearest == null)
                    return null;
                string yAxisLabel = GraphPane.YAxis.Title.Text ?? string.Empty;
                table.AddDetailRow(GraphsResources.GraphFullScan_ToolTip_mz,
                    nearest.Point.X.ToString(Formats.Mz), rt);
                table.AddDetailRow(yAxisLabel,
                    nearest.Point.Y.ToString(Formats.IonMobility), rt);
                table.AddDetailRow(GraphsResources.GraphFullScan_ToolTip_Intensity,
                    nearest.Point.Z.ToString(@"F0"), rt);
            }
            else
            {
                var specPane = IsDualPane ? _stickSpectrumPane : GraphPane;
                if (specPane.CurveList.Count == 0)
                    return null;
                var curve = specPane.CurveList[0];
                if (curve.Points.Count == 0)
                    return null;
                // Find curve point nearest to target m/z (or use midpoint)
                double targetMz = double.IsNaN(x)
                    ? curve.Points[curve.Points.Count / 2].X : x;
                int bestIndex = 0;
                double bestDist = double.MaxValue;
                for (int i = 0; i < curve.Points.Count; i++)
                {
                    double dist = Math.Abs(curve.Points[i].X - targetMz);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestIndex = i;
                    }
                }
                var dataPoint = curve.Points[bestIndex];
                table.AddDetailRow(GraphsResources.GraphFullScan_ToolTip_mz,
                    dataPoint.X.ToString(Formats.Mz), rt);
                table.AddDetailRow(GraphsResources.GraphFullScan_ToolTip_Intensity,
                    dataPoint.Y.ToString(@"F0"), rt);
            }
            return table;
        }

        public string TestGetTooltipText(double x = double.NaN, double y = double.NaN)
        {
            return TestGetTooltipTable(x, y)?.ToString();
        }

        /// <summary>
        /// Returns the mobilogram tooltip table for the nearest PlotY2D point to the given ion
        /// mobility value. Skips the screen-coordinate proximity check used during interaction.
        /// </summary>
        public TableDesc TestGetMobilogramTooltipTable(double imValue = double.NaN)
        {
            if (!IsMobilogramVisible || _heatMapData?.PlotY2D == null || _heatMapData.PlotY2D.Count == 0)
                return null;
            double searchIm = double.IsNaN(imValue)
                ? _heatMapData.PlotY2D[_heatMapData.PlotY2D.Count / 2].Key
                : imValue;
            var nearest = _heatMapData.PlotY2D
                .OrderBy(kvp => Math.Abs(kvp.Key - searchIm))
                .First();
            var rt = _cursorTip.RenderTools;
            string yAxisLabel = GraphPane.YAxis.Title.Text ?? string.Empty;
            var table = new TableDesc();
            table.AddDetailRow(yAxisLabel, nearest.Key.ToString(Formats.IonMobility), rt);
            table.AddDetailRow(GraphsResources.GraphFullScan_ToolTip_Summed_Intensity, nearest.Value.ToString(@"F0"), rt);
            return table;
        }

        public string TitleText => IsDualPane ? _stickSpectrumPane.Title.Text : GraphPane.Title.Text;
        public double XAxisMin => GraphPane.XAxis.Scale.Min;
        public double XAxisMax => GraphPane.XAxis.Scale.Max;
        public double YAxisMin => GraphPane.YAxis.Scale.Min;
        public double YAxisMax => GraphPane.YAxis.Scale.Max;
        // In dual-pane mode, expose stick pane's Y-axis (intensity) separately from heatmap Y-axis (ion mobility)
        public double StickYAxisMin => _stickSpectrumPane?.YAxis.Scale.Min ?? GraphPane.YAxis.Scale.Min;
        public double StickYAxisMax => _stickSpectrumPane?.YAxis.Scale.Max ?? GraphPane.YAxis.Scale.Max;
        public bool IsDualPaneMode => IsDualPane;
        // True if the purple ion-mobility filter band is currently drawn on the heatmap.
        public bool HasIonMobilityFilterBand => GraphPane.GraphObjList.OfType<BoxObj>().Any(b => TAG_IM_FILTER_BAND.Equals(b.Tag));

        public bool IsScanTypeSelected(ChromSource source)
        {
            return string.Equals(comboBoxScanType.SelectedItem.ToString(),
                _msDataFileScanHelper.NameFromSource(source));
        }

        public void SelectScanType(ChromSource source)
        {
            comboBoxScanType.SelectedItem = _msDataFileScanHelper.NameFromSource(source);
        }

        public void SetFilter(bool isChecked)
        {
            filterBtn.Checked = isChecked;
        }

        public void SetZoom(bool isChecked)
        {
            magnifyBtn.Checked = isChecked;
        }

        public void SetSpectrum(bool isChecked)
        {
            spectrumBtn.Checked = isChecked;
        }

        public void SetShowAnnotations(bool isChecked)
        {
            toolStripButtonShowAnnotations.Checked = isChecked;
        }

        public void SetPeakTypeSelection(PeakType peakType)
        {
            comboBoxPeakType.SelectedItem = _msDataFileScanHelper.GetPeakTypeLocalizedName(peakType);
        }

        public MsDataFileScanHelper MsDataFileScanHelper
        {
            get => _msDataFileScanHelper;
        }

        public IEnumerable<string> IonLabels
        {
            get
            {
                var labelPane = IsDualPane ? _stickSpectrumPane : GraphPane;
                if (toolStripButtonShowAnnotations.Checked && Program.SkylineOffscreen)
                {
                    var annotationCurves = labelPane.CurveList.FindAll(c => c is StickItem && c.Tag is SpectrumGraphItem);
                    if (annotationCurves.Any())
                    {
                        var graphItem = annotationCurves.First().Tag as SpectrumGraphItem;
                        return graphItem?.IonLabels;
                    }
                    else
                        return null;
                }
                else
                {
                    return labelPane.GraphObjList.OfType<TextObj>()
                        .ToList().FindAll(txt => txt.Location.X >= XAxisMin && txt.Location.X <= XAxisMax)
                        .Select(label => label.Text).ToHashSet();
                }
            }
        }

        public void SetMobilogram(bool isChecked)
        {
            mobilogramBtn.Checked = isChecked;
        }

        public bool IsMobilogramVisible => mobilogramBtn.Visible && mobilogramBtn.Checked;

        public ToolStripButton PropertyButton => propertiesBtn;
        public ToolStripButton LeftButton => leftButton;
        public MsGraphExtension MsGraphExtension => graphControlExtension;

        #endregion Test support

        private void showScanNumberToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.ShowFullScanNumber = !Settings.Default.ShowFullScanNumber;
            UpdateUI();
        }

        private void showCollisionEnergyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.ShowFullScanCE = !Settings.Default.ShowFullScanCE;
            UpdateUI();
        }

        private void toolStripButtonShowAnnotations_CheckedChanged(object sender, EventArgs e)
        {
            _showIonSeriesAnnotations = toolStripButtonShowAnnotations.Checked;
            showPeakAnnotationsContextMenuItem.Checked = Settings.Default.ShowFullScanAnnotations = _showIonSeriesAnnotations;
            UpdateUI();
        }

        private void showIonTypesRanksToolStripMenuItem_CheckedChanged(object sender, EventArgs e)
        {
            _showIonSeriesAnnotations = showPeakAnnotationsContextMenuItem.Checked;
            toolStripButtonShowAnnotations.Checked = Settings.Default.ShowFullScanAnnotations = _showIonSeriesAnnotations;
            UpdateUI();
        }

        private void comboBoxPeakType_SelectedIndexChanged(object sender, EventArgs e)
        {
            Settings.Default.FullScanPeakType = _msDataFileScanHelper.PeakTypeFromLocalizedName((string) comboBoxPeakType.SelectedItem).ToString();
            LoadScan(false, true);
        }
    }

    public class SpectrumItem : AbstractMSGraphItem
    {
        private readonly IPointList _points;
        private readonly Color _color;
        private readonly string _title;

        public SpectrumItem(IPointList points, Color color, string title, float width = 1)
        {
            _points = points;
            _color = color;
            _title = title;
            LineWidth = Settings.Default.SpectrumLineWidth*width;
        }

        public override string Title { get { return _title; } }

        public override Color Color
        {
            get { return _color; }
        }

        public override PointAnnotation AnnotatePoint(PointPair point)
        {
            return null;
        }

        public override void AddAnnotations(MSGraphPane graphPane, Graphics g,
            MSPointList pointList, GraphObjList annotations)
        {
        }

        public override void AddPreCurveAnnotations(MSGraphPane graphPane, Graphics g,
            MSPointList pointList, GraphObjList annotations)
        {
        }

        public override IPointList Points { get { return _points; } }
    }

    public class SpectrumShadeItem : SpectrumItem
    {
        public SpectrumShadeItem(IPointList points, Color color, string title)
            : base(points, color, title)
        {
        }

        public override MSGraphItemDrawMethod GraphItemDrawMethod
        {
            get { return MSGraphItemDrawMethod.fill; }
        }

    }

    public sealed class SelectedScanEventArgs : EventArgs
    {
        public SelectedScanEventArgs(MsDataFileUri dataFile, double retentionTime, Identity transitionId, int? optStep)
        {
            DataFile = dataFile;
            RetentionTime = retentionTime;
            TransitionId = transitionId;
            OptStep = optStep;
        }

        public MsDataFileUri DataFile { get; private set; }
        public double RetentionTime { get; private set; }
        public Identity TransitionId { get; private set; }
        public int? OptStep { get; }
    }
}
