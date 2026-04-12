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
        private const float MOBILOGRAM_LINE_WIDTH = 1.5f;
        private const float MOBILOGRAM_GAP = 18f;
        private const float MOBILOGRAM_RIGHT_PAD = 18f;
        private const string TAG_IM_FILTER_BAND = "IMFilterBand";

        private readonly IDocumentUIContainer _documentContainer;
        private readonly GraphHelper _graphHelper;
        private HeatMapData _heatMapData;
        private HeatMapGraphPane _heatMapPane;
        private MSGraphPane _stickSpectrumPane;
        private bool IsDualPane => _stickSpectrumPane != null && graphControl.MasterPane.PaneList.Count == 2;
        /// <summary>
        /// Compute right margin so mobilogram is approximately 1/5 of the window width.
        /// </summary>
        private float MobilogramMargin
        {
            get
            {
                float scaleFactor = _heatMapPane.CalcScaleFactor();
                return scaleFactor > 0 ? graphControl.Width / (5f * scaleFactor) : 150f;
            }
        }
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
        /// </summary>
        private void SetupDualPaneLayout()
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

            var mp = graphControl.MasterPane;
            if (mp.PaneList.Count == 2 &&
                ReferenceEquals(mp.PaneList[0], _stickSpectrumPane) &&
                ReferenceEquals(mp.PaneList[1], _heatMapPane))
                return; // Already set up

            mp.PaneList.Clear();
            mp.InnerPaneGap = 0;
            // Tighten gap between panes while keeping enough room for Y-axis labels
            _stickSpectrumPane.Margin.Bottom = 0;
            _heatMapPane.Margin.Top = 10;
            using (var g = graphControl.CreateGraphics())
            {
                mp.SetLayout(g, true, new[] { 1, 1 }, new[] { 0.45f, 0.55f });
                mp.Add(_stickSpectrumPane);
                mp.Add(_heatMapPane);
                mp.DoLayout(g);
            }
            graphControl.IsSynchronizeXAxes = true;
        }

        /// <summary>
        /// Restore single-pane layout (heatmap only or stick only).
        /// </summary>
        private void SetupSinglePaneLayout()
        {
            _stickSpectrumPane = null;
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
                    SetupDualPaneLayout();

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

                    GetRankedSpectrum();

                    // Bottom pane: heatmap + mobilogram
                    bool showMobilogram = mobilogramBtn.Checked;
                    float rightMargin = showMobilogram
                        ? ZedGraph.Margin.Default.Right + MobilogramMargin
                        : ZedGraph.Margin.Default.Right;
                    _heatMapPane.Margin.Right = rightMargin;
                    _stickSpectrumPane.Margin.Right = rightMargin;
                    ZoomHeatMapYAxis();
                    CreateIonMobilityHeatmap();
                    if (showMobilogram)
                        CreateMobilogram();

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
                                     (imFilter.IonMobilityFilterWindow.Offset ?? 0);
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
                                           (transition.IonMobilityInfo.IonMobilityFilterWindow.Offset ?? 0);
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
            FireZoomEvent(newState);
        }

        private void graphControl_Resize(object sender, EventArgs e)
        {
            if (mobilogramBtn.Visible && mobilogramBtn.Checked)
            {
                float rightMargin = ZedGraph.Margin.Default.Right + MobilogramMargin;
                _heatMapPane.Margin.Right = rightMargin;
                if (IsDualPane)
                    _stickSpectrumPane.Margin.Right = rightMargin;
            }
            if (IsDualPane)
            {
                using (var g = graphControl.CreateGraphics())
                    graphControl.MasterPane.DoLayout(g);
            }
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
            if (_heatMapData?.PlotY2D == null)
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
                                     (imFilter.IonMobilityFilterWindow.Offset ?? 0);
                }
            }

            var overlay = new MobilogramOverlay(_heatMapData.PlotY2D, filterMin, filterMax, filterPeak)
            {
                ZOrder = ZOrder.A_InFront
            };
            GraphPane.GraphObjList.Add(overlay);
        }

        /// <summary>
        /// Custom GraphObj that draws a mobilogram (summed intensity vs ion mobility) in the
        /// left margin area of the heatmap pane. Because it uses the pane's own Y-axis transform,
        /// the Y-axes are guaranteed to align pixel-for-pixel.
        /// </summary>
        private class MobilogramOverlay : GraphObj
        {
            private readonly List<KeyValuePair<float, double>> _plotData;
            private readonly double _filterMin, _filterMax, _filterPeak;

            public MobilogramOverlay(List<KeyValuePair<float, double>> plotData,
                double filterMin, double filterMax, double filterPeak)
                : base(0, 0)
            {
                _plotData = plotData;
                _filterMin = filterMin;
                _filterMax = filterMax;
                _filterPeak = filterPeak;
            }

            public override void Draw(Graphics g, PaneBase paneBase, float scaleFactor)
            {
                var pane = paneBase as GraphPane;
                if (pane == null || _plotData == null || _plotData.Count == 0)
                    return;

                var chartRect = pane.Chart.Rect;
                if (chartRect.Width <= 0 || chartRect.Height <= 0)
                    return;

                // Gap between heatmap chart edge and mobilogram plot area
                float gapWidth = MOBILOGRAM_GAP * scaleFactor;

                // Mobilogram plot area: right margin space, vertically aligned with chart rect
                // Reserve space on right so rightmost tick label can be centered on its tick
                float rightLabelPad = MOBILOGRAM_RIGHT_PAD * scaleFactor;
                float mobLeft = chartRect.Right + gapWidth;
                float mobRight = paneBase.Rect.Right - rightLabelPad;
                float mobTop = chartRect.Top;
                float mobBottom = chartRect.Bottom; // Same bottom as heatmap chart area
                float mobWidth = mobRight - mobLeft;
                float plotBottom = mobBottom;
                float plotHeight = plotBottom - mobTop;

                if (mobWidth < 10 || plotHeight < 10)
                    return;

                // Find max intensity for X scaling
                double maxIntensity = 0;
                foreach (var kvp in _plotData)
                {
                    if (kvp.Value > maxIntensity)
                        maxIntensity = kvp.Value;
                }
                if (maxIntensity <= 0)
                    return;

                // Compute magnitude and nice round max (same approach as ZedGraph LinearScale.PickScale)
                int mag = (int)Math.Floor(Math.Log10(maxIntensity));
                mag = Math.Max(0, (mag / 3) * 3);
                double scaleMult = mag > 0 ? Math.Pow(10.0, mag) : 1.0;
                double scaledRawMax = maxIntensity / scaleMult;
                // Pick a nice step size targeting ~4 major divisions
                double xMajorStep = CalcNiceStepSize(scaledRawMax, 4);
                double xMinorStep = CalcNiceStepSize(xMajorStep, 5);
                // Round max up to next major step
                double scaledMax = xMajorStep * Math.Ceiling(scaledRawMax / xMajorStep);
                double displayMax = scaledMax * scaleMult; // unscaled max for curve drawing

                var savedState = g.Save();

                // Get axis styling from the heatmap pane for consistent appearance
                var yAxis = pane.YAxis;
                var xAxis = pane.XAxis;
                var axisColor = yAxis.Color;
                var majorTicSize = yAxis.MajorTic.ScaledTic(scaleFactor);
                var minorTicSize = yAxis.MinorTic.ScaledTic(scaleFactor);
                var axisPenWidth = pane.ScaledPenWidth(yAxis.MajorTic.PenWidth, scaleFactor);
                var minorPenWidth = pane.ScaledPenWidth(yAxis.MinorTic.PenWidth, scaleFactor);
                var xLabelFontSpec = xAxis.Scale.FontSpec;
                var charHeight = xLabelFontSpec.GetHeight(scaleFactor);

                var plotRect = new RectangleF(mobLeft, mobTop, mobWidth, plotHeight);

                // Draw left and bottom axis lines (L-shape, no full border box)
                using (var framePen = new Pen(axisColor, axisPenWidth))
                {
                    g.DrawLine(framePen, mobLeft, mobTop, mobLeft, mobTop + plotHeight);
                    g.DrawLine(framePen, mobLeft, mobTop + plotHeight, mobLeft + mobWidth, mobTop + plotHeight);
                }

                // Clip to plot area for data drawing
                g.SetClip(plotRect);

                // Draw filter region (behind curve)
                if (!double.IsNaN(_filterMin) && !double.IsNaN(_filterMax))
                {
                    float filterTopPx = yAxis.Scale.Transform(_filterMax);
                    float filterBottomPx = yAxis.Scale.Transform(_filterMin);
                    var filterRect = new RectangleF(mobLeft, filterTopPx,
                        mobWidth, filterBottomPx - filterTopPx);

                    using (var fillBrush = new SolidBrush(Color.FromArgb(50, Color.Gray)))
                        g.FillRectangle(fillBrush, filterRect);
                    using (var borderPen = new Pen(Color.FromArgb(100, Color.DarkViolet), 2))
                        g.DrawRectangle(borderPen, filterRect.X, filterRect.Y,
                            filterRect.Width, filterRect.Height);
                }

                // Build polygon points for filled curve (scaled to displayMax for nice round axis)
                var polyPoints = new List<PointF>(_plotData.Count + 2);
                polyPoints.Add(new PointF(mobLeft, yAxis.Scale.Transform(_plotData[0].Key)));

                foreach (var kvp in _plotData)
                {
                    float px = mobLeft + (float)(kvp.Value / displayMax) * mobWidth;
                    float py = yAxis.Scale.Transform(kvp.Key);
                    polyPoints.Add(new PointF(px, py));
                }

                polyPoints.Add(new PointF(mobLeft,
                    yAxis.Scale.Transform(_plotData[_plotData.Count - 1].Key)));

                // Fill under curve
                using (var fillBrush = new SolidBrush(Color.FromArgb(40, Color.DarkBlue)))
                    g.FillPolygon(fillBrush, polyPoints.ToArray());

                // Draw curve line
                if (polyPoints.Count > 3)
                {
                    var linePoints = polyPoints.GetRange(1, polyPoints.Count - 2).ToArray();
                    using (var linePen = new Pen(Color.DarkBlue, MOBILOGRAM_LINE_WIDTH))
                        g.DrawLines(linePen, linePoints);
                }

                // Draw peak center line
                if (!double.IsNaN(_filterPeak))
                {
                    float peakPx = yAxis.Scale.Transform(_filterPeak);
                    using (var dashPen = new Pen(Color.FromArgb(180, Color.DarkViolet), 1.5f))
                    {
                        dashPen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                        g.DrawLine(dashPen, mobLeft, peakPx, mobRight, peakPx);
                    }
                }

                g.Restore(savedState);

                // Draw X-axis major and minor ticks protruding downward (matching ZedGraph bottom-axis style),
                // with labels positioned using ZedGraph's standard LabelGap (Scale.Default.LabelGap = 0.3f).
                var xMajorTicSize = xAxis.MajorTic.ScaledTic(scaleFactor);
                var xMinorTicSize = xAxis.MinorTic.ScaledTic(scaleFactor);
                using (var majorPen = new Pen(axisColor, axisPenWidth))
                using (var minorPen = new Pen(axisColor, minorPenWidth))
                {
                    float tickY = plotBottom;
                    float labelY = tickY + xMajorTicSize + charHeight * 0.3f;
                    string tickFormat = scaledMax >= 10 ? @"0" :
                        scaledMax >= 1 ? @"0.#" : @"0.##";

                    // Draw minor ticks protruding downward
                    if (xMinorStep > 0 && scaledMax / xMinorStep < 100)
                    {
                        for (double val = xMinorStep; val < scaledMax; val += xMinorStep)
                        {
                            float px = mobLeft + (float)(val / scaledMax) * mobWidth;
                            g.DrawLine(minorPen, px, tickY, px, tickY + xMinorTicSize);
                        }
                    }

                    // Draw major ticks protruding downward with labels centered below
                    for (double val = 0; val <= scaledMax + xMajorStep * 0.001; val += xMajorStep)
                    {
                        float px = mobLeft + (float)(val / scaledMax) * mobWidth;

                        g.DrawLine(majorPen, px, tickY, px, tickY + xMajorTicSize);

                        string label = val.ToString(tickFormat);
                        xLabelFontSpec.Draw(g, paneBase, label, px, labelY,
                            AlignH.Center, AlignV.Top, scaleFactor);
                    }

                    // Draw axis title with magnitude indicator, bold to match heatmap axis titles
                    float titleY = labelY + charHeight + charHeight * 0.3f;
                    float titleX = (mobLeft + mobRight) / 2;
                    string title = mag > 0
                        ? string.Format(@"{0} (10^{1})",
                            GraphsResources.AbstractMSGraphItem_CustomizeYAxis_Intensity, mag)
                        : GraphsResources.AbstractMSGraphItem_CustomizeYAxis_Intensity;
                    var titleFontSpec = (FontSpec)xAxis.Title.FontSpec.Clone();
                    titleFontSpec.IsItalic = false; // Override italic (only m/z is italic)
                    titleFontSpec.Draw(g, paneBase, title,
                        titleX, titleY, AlignH.Center, AlignV.Top, scaleFactor);
                }

                // Draw Y-axis major and minor ticks straddling the left border
                var yScale = yAxis.Scale;
                double yMin = yScale.Min;
                double yMax = yScale.Max;
                double yMajorStep = yScale.MajorStep;
                double yMinorStep = yScale.MinorStep;
                if (yMajorStep > 0 && (yMax - yMin) / yMajorStep < 100)
                {
                    using (var majorPen = new Pen(axisColor, axisPenWidth))
                    using (var minorPen = new Pen(axisColor, minorPenWidth))
                    {
                        // Draw minor ticks protruding right only (into mobilogram area)
                        if (yMinorStep > 0 && (yMax - yMin) / yMinorStep < 500)
                        {
                            double firstMinor = Math.Ceiling(yMin / yMinorStep) * yMinorStep;
                            for (double val = firstMinor; val <= yMax; val += yMinorStep)
                            {
                                float py = yAxis.Scale.Transform(val);
                                if (py < mobTop || py > plotBottom)
                                    continue;
                                g.DrawLine(minorPen, mobLeft, py, mobLeft + minorTicSize, py);
                            }
                        }

                        // Draw major ticks protruding right only (into mobilogram area)
                        double firstMajor = Math.Ceiling(yMin / yMajorStep) * yMajorStep;
                        for (double val = firstMajor; val <= yMax; val += yMajorStep)
                        {
                            float py = yAxis.Scale.Transform(val);
                            if (py < mobTop || py > plotBottom)
                                continue;
                            g.DrawLine(majorPen, mobLeft, py, mobLeft + majorTicSize, py);
                        }
                    }
                }
            }

            /// <summary>
            /// Calculate a nice step size for a given range and target number of steps.
            /// Same algorithm as ZedGraph Scale.CalcStepSize: promotes MSD to 1, 2, or 5.
            /// </summary>
            private static double CalcNiceStepSize(double range, double targetSteps)
            {
                double tempStep = range / targetSteps;
                double mag = Math.Floor(Math.Log10(tempStep));
                double magPow = Math.Pow(10.0, mag);
                double magMsd = (int)(tempStep / magPow + 0.5);

                if (magMsd > 5.0)
                    magMsd = 10.0;
                else if (magMsd > 2.0)
                    magMsd = 5.0;
                else if (magMsd > 1.0)
                    magMsd = 2.0;

                return magMsd * magPow;
            }

            public override bool PointInBox(PointF pt, PaneBase pane, Graphics g, float scaleFactor)
            {
                return false;
            }

            public override void GetCoords(PaneBase pane, Graphics g, float scaleFactor,
                out string shape, out string coords)
            {
                shape = string.Empty;
                coords = string.Empty;
            }

            public override void GetObjectData(SerializationInfo info, StreamingContext context)
            {
            }
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
            var chartRect = _heatMapPane.Chart.Rect;
            float scaleFactor = _heatMapPane.CalcScaleFactor();
            float gapWidth = MOBILOGRAM_GAP * scaleFactor;
            float mobLeft = chartRect.Right + gapWidth;
            float mobRight = _heatMapPane.Rect.Right - MOBILOGRAM_RIGHT_PAD * scaleFactor;
            return pt.X >= mobLeft && pt.X <= mobRight &&
                   pt.Y >= chartRect.Top && pt.Y <= chartRect.Bottom;
        }

        private TableDesc GetMobilogramTooltipTable(PointF pt)
        {
            if (_heatMapData?.PlotY2D == null || _heatMapData.PlotY2D.Count == 0)
                return null;

            double imValue;
            GraphPane.ReverseTransform(pt, out _, out imValue);

            // Find nearest point in the mobilogram by ion mobility
            var nearest = _heatMapData.PlotY2D
                .OrderBy(kvp => Math.Abs(kvp.Key - imValue))
                .First();

            // Compute the curve's pixel X for the nearest point to check proximity
            var chartRect = _heatMapPane.Chart.Rect;
            float scaleFactor = _heatMapPane.CalcScaleFactor();
            float mobLeft = chartRect.Right + 6 * scaleFactor;
            float mobRight = _heatMapPane.Rect.Right - MOBILOGRAM_RIGHT_PAD * scaleFactor;
            float mobWidth = mobRight - mobLeft;
            double maxIntensity = _heatMapData.PlotY2D.Max(kvp => kvp.Value);
            float curveX = maxIntensity > 0
                ? mobLeft + (float)(nearest.Value / maxIntensity) * mobWidth
                : mobLeft;

            // Show tooltip only if cursor is within the filter band, or within 3x the line width of the curve tip.
            // The filled area already provides a generous hit zone; this tolerance just covers
            // rendering slop at the curve edge (unlike the heatmap's 10px which searches a point cloud).
            float searchPixels = 3f * MOBILOGRAM_LINE_WIDTH * scaleFactor;
            double filterMin, filterMax;
            _msDataFileScanHelper.GetIonMobilityFilterDisplayRange(out filterMin, out filterMax, _msDataFileScanHelper.Source);
            bool inFilterBand = filterMin > 0 && filterMax < double.MaxValue &&
                                imValue >= filterMin && imValue <= filterMax;
            if (!inFilterBand && pt.X > curveX + searchPixels)
                return null;

            var rt = _cursorTip.RenderTools;
            string yAxisLabel = GraphPane.YAxis.Title.Text ?? string.Empty;
            var table = new TableDesc();
            table.AddDetailRow(yAxisLabel, nearest.Key.ToString(Formats.IonMobility), rt);
            table.AddDetailRow(GraphsResources.GraphFullScan_ToolTip_Summed_Intensity, nearest.Value.ToString(@"F0"), rt);
            return table;
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
