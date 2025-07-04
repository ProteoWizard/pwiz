﻿/*
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

        private readonly IDocumentUIContainer _documentContainer;
        private readonly GraphHelper _graphHelper;
        private HeatMapData _heatMapData;
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

        private MSGraphControl graphControl => graphControlExtension.Graph;

        public GraphFullScan(IDocumentUIContainer documentUIContainer)
        {
            InitializeComponent();

            graphControl.GraphPane = new HeatMapGraphPane
            {
                MinDotRadius = MIN_DOT_RADIUS,
                MaxDotRadius = MAX_DOT_RADIUS,
                ShowHeatMap = !Settings.Default.SumScansFullScan
            };
            graphControl.GraphPane.AllowLabelOverlap = true;
            graphControl.ContextMenuBuilder += graphControl_ContextMenuBuilder;
            graphControl.MouseMoveEvent += graphControl_MouseMove;
            graphControl.MouseClick += graphControl_MouseClick;
            graphControl.ZoomEvent += graphControl_ZoomEvent;

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

            magnifyBtn.CheckedChanged += magnifyBtn_CheckedChanged;
            spectrumBtn.CheckedChanged += spectrumBtn_CheckedChanged;
            filterBtn.CheckedChanged += filterBtn_CheckedChanged;
            toolStripButtonShowAnnotations.CheckedChanged += toolStripButtonShowAnnotations_CheckedChanged;


            spectrumBtn.Visible = false;
            filterBtn.Visible = false;
            lblScanId.Visible = false; // you might want to show the scan index for debugging
            comboBoxPeakType.Items.Clear();
            comboBoxPeakType.Items.Add(_msDataFileScanHelper.GetPeakTypeLocalizedName(PeakType.chromDefault));
            comboBoxPeakType.Items.Add(_msDataFileScanHelper.GetPeakTypeLocalizedName(PeakType.centroided));
            comboBoxPeakType.Items.Add(_msDataFileScanHelper.GetPeakTypeLocalizedName(PeakType.profile));
            var peakType = _msDataFileScanHelper.ParsePeakTypeEnumName(Settings.Default.FullScanPeakType);
            comboBoxPeakType.SelectedItem = _msDataFileScanHelper.GetPeakTypeLocalizedName(peakType);
            this.comboBoxPeakType.SelectedIndexChanged += this.comboBoxPeakType_SelectedIndexChanged;
            graphControlExtension.RestorePropertiesSheet();
        }

        public ZedGraphControl ZedGraphControl
        {
            get { return graphControl; }
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
            get { return (MSGraphPane) graphControl.MasterPane[0]; }
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
        /// Create the heat map or single scan graph.
        /// </summary>
        private void CreateGraph()
        {
            if (_msDataFileScanHelper.MsDataSpectra == null)
                return;

            GraphPane.CurveList.Clear();
            GraphPane.GraphObjList.Clear();
            if(!toolStripButtonShowAnnotations.Checked)
                _rmis = null;

            bool hasIonMobilityDimension = _msDataFileScanHelper.MsDataSpectra.Length > 1 ||
                                           _msDataFileScanHelper.MsDataSpectra.First().IonMobilities != null;
            bool useHeatMap = hasIonMobilityDimension && !Settings.Default.SumScansFullScan;

            filterBtn.Visible = spectrumBtn.Visible = hasIonMobilityDimension;
            graphControl.IsEnableVPan = graphControl.IsEnableVZoom = useHeatMap;
            GraphPane.Legend.IsVisible = useHeatMap;

            if (spectrumBtn.Visible)
                toolStripButtonShowAnnotations.Visible = (_msDataFileScanHelper.Source == ChromSource.fragment) &&
                                                         (!spectrumBtn.Visible ||
                                                          spectrumBtn.Visible && spectrumBtn.Checked);
            else
                toolStripButtonShowAnnotations.Visible = (_msDataFileScanHelper.Source == ChromSource.fragment);

            _showIonSeriesAnnotations = toolStripButtonShowAnnotations.Visible && Settings.Default.ShowFullScanAnnotations;

            if (hasIonMobilityDimension)
            {
                // Is there actually any drift time filtering available?
                double minIonMobilityFilter, maxIonMobilityFilter;
                _msDataFileScanHelper.GetIonMobilityFilterRange(out minIonMobilityFilter, out maxIonMobilityFilter, ChromSource.unknown); // Get range of IM values for all products and precursors

                if ((minIonMobilityFilter == double.MinValue) && (maxIonMobilityFilter == double.MaxValue))
                {
                    filterBtn.Visible = false;
                    filterBtn.Checked = false;
                }
            }

            GetRankedSpectrum();
            double[] massErrors = null;
            if (useHeatMap)
            {
                ZoomYAxis(); // Call this again now that cues are there to indicate need for drift scale
                CreateIonMobilityHeatmap();
            }
            else
            {
                CreateSingleScan(out massErrors);
            }

            PopulateProperties();

            // Add extraction boxes.
            for (int i = 0; i < _msDataFileScanHelper.ScanProvider.Transitions.Length; i++)
            {
                var transition = _msDataFileScanHelper.ScanProvider.Transitions[i];
                if (transition.Source != _msDataFileScanHelper.Source)
                    continue;
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
                GraphPane.GraphObjList.Add(extractionBox);
            }

            // Add labels.
            if (!_showIonSeriesAnnotations)
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
                    GraphPane.GraphObjList.Add(label);
                }
            }

            double retentionTime = _msDataFileScanHelper.MsDataSpectra[0].RetentionTime ?? _msDataFileScanHelper.ScanProvider.Times[_msDataFileScanHelper.ScanIndex];
            GraphPane.Title.Text = string.Format(Resources.GraphFullScan_CreateGraph__0_____1_F2__min_, _msDataFileScanHelper.FileName, retentionTime);

            FireSelectedScanChanged(retentionTime);
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
                _heatMapData = new HeatMapData(points);
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
                    Border = new Border(Color.FromArgb(100, Color.DarkViolet), 2)
                };
                GraphPane.GraphObjList.Add(driftTimeOutline);
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
                            spectrumProperties.IonMobility = TextUtil.SpaceSeparate(imAndCss.IonMobility.Mobility.Value.ToString(Formats.IonMobility),
                                imAndCss.IonMobility.UnitsString);
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
        private void  CreateSingleScan(out double[] massErrors)
        {
            GraphPane.YAxis.Title.Text = GraphsResources.AbstractMSGraphItem_CustomizeYAxis_Intensity;
            graphControl.IsEnableVZoom = graphControl.IsEnableVPan = false;
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
                    _graphHelper.AddSpectrum(graphItem, false);
                }

                else
                {
                    // No node to use for annotation so just show peaks in gray
                    var item = new SpectrumItem(allPointList, Color.Gray, @"unmatched");
                    var curveItem = _graphHelper.GraphControl.AddGraphItem(GraphPane, item, false);
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
                    var curveItem = _graphHelper.GraphControl.AddGraphItem(GraphPane, item, false);
                    curveItem.Label.IsVisible = false;
                }

                // Add points that aren't associated with a transition.
                {
                    var item = new SpectrumItem(defaultPointList, Color.Gray, @"unmatched");
                    var curveItem = _graphHelper.GraphControl.AddGraphItem(GraphPane, item, false);
                    curveItem.Label.IsVisible = false;
                }
            }
            // Create curve for all points to provide shading behind stick graph.
            if (_msDataFileScanHelper.MsDataSpectra.Length > 0 && !_msDataFileScanHelper.MsDataSpectra[0].Centroided)
            {
                var item = new SpectrumShadeItem(allPointList, Color.FromArgb(100, 225, 225, 150), @"all");
                var curveItem = _graphHelper.GraphControl.AddGraphItem(GraphPane, item, false);
                curveItem.Label.IsVisible = false;
            }
            GraphPane.SetScale(CreateGraphics());

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
            graphControl.GraphPane.Title.Text = _msDataFileScanHelper.FileName;
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

            var xScale = GraphPane.XAxis.Scale;
            xScale.MinAuto = xScale.MaxAuto = false;

            if (magnifyBtn.Checked)
            {
                double mz = _msDataFileScanHelper.Source == ChromSource.ms1
                    ? _msDataFileScanHelper.ScanProvider.Transitions[_msDataFileScanHelper.TransitionIndex].PrecursorMz
                    : _msDataFileScanHelper.ScanProvider.Transitions[_msDataFileScanHelper.TransitionIndex].ProductMz;
                xScale.Min = mz - 1.5;
                xScale.Max = mz + 3.5;
            }
            else
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
                GraphPane.SetScale(g);

            graphControl.Refresh();
        }

        public void SetIntensityScale(double maxIntensity)
        {
            GraphPane.YAxis.Scale.MaxAuto = false;
            GraphPane.YAxis.Scale.Max = maxIntensity;
            GraphPane.AxisChange();
        }

        public MzRange Range
        {
            get { return new MzRange(GraphPane.XAxis.Scale.Min, GraphPane.XAxis.Scale.Max); }
        }

        public void ApplyMZZoomState(ZoomState newState)
        {
            newState.XAxis.ApplyScale(GraphPane.XAxis);
            using (var g = graphControl.CreateGraphics())
                GraphPane.SetScale(g);
            graphControl.Refresh();
        }

        public event EventHandler<ZoomEventArgs> ZoomEvent;

        private void graphControl_ZoomEvent(ZedGraphControl sender, ZoomState oldState, ZoomState newState, PointF mousePosition)
        {
            FireZoomEvent(newState);
        }

        private void FireZoomEvent(ZoomState zoomState = null)
        {
            if (ZoomEvent != null && Settings.Default.SyncMZScale)
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
                _msDataFileScanHelper.GetIonMobilityFilterDisplayRange(out minDriftTime, out maxDriftTime, _msDataFileScanHelper.Source);
                if (minDriftTime > double.MinValue && maxDriftTime < double.MaxValue)
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

        public void UpdateUI(bool selectionChanged = true)
        {
            // Only worry about updates, if the graph is visible
            // And make sure it is not disposed, since rendering happens on a timer
            if (!Visible || IsDisposed || _msDataFileScanHelper.ScanProvider == null)
                return;
            GraphHelper.FormatGraphPane(graphControl.GraphPane);
            comboBoxPeakType.Visible = spectrumBtn.Checked;
            toolStripLabelPeakType.Visible = spectrumBtn.Checked;

            if (selectionChanged)
                CreateGraph();

            if (_msDataFileScanHelper.MsDataSpectra != null)
            {
                leftButton.Enabled = (_msDataFileScanHelper.ScanIndex > 0);
                rightButton.Enabled = (_msDataFileScanHelper.ScanIndex < _msDataFileScanHelper.ScanProvider.Times.Count-1);
                lblScanId.Text = _msDataFileScanHelper.GetScanIndex().ToString(@"D");
                if (!spectrumBtn.Checked)
                    GraphPane.SetScale(CreateGraphics());
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
            _documentContainer.UnlistenUI(OnDocumentUIChanged);
            _msDataFileScanHelper.Dispose();
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
            UpdateUI();
            ZoomYAxis();
            graphControl.Invalidate();
        }

        private HeatMapGraphPane HeatMapGraphPane { get { return (HeatMapGraphPane) GraphPane; } }

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
            var nearestLabel = GetNearestLabel(new PointF(e.X, e.Y));
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
            var nearestLabel = GetNearestLabel(pt);
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

        private TextObj GetNearestLabel(PointF mousePoint)
        {
            using (Graphics g = CreateGraphics())
            {
                object nearestObject;
                if (GraphPane.FindNearestObject(mousePoint, g, out nearestObject, out _))
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
            var mouse = TransformCoordinates(x, y);
            graphControl_MouseClick(null, new MouseEventArgs(MouseButtons.Left, 1, (int)mouse.X, (int)mouse.Y, 0));
        }

        public PointF TransformCoordinates(double x, double y, CoordType coordType = CoordType.AxisXYScale)
        {
            return GraphPane.GeneralTransform(new PointF((float)x, (float)y), coordType);
        }

        public string TitleText { get { return GraphPane.Title.Text; } }
        public double XAxisMin { get { return GraphPane.XAxis.Scale.Min; }}
        public double XAxisMax { get { return GraphPane.XAxis.Scale.Max; }}
        public double YAxisMin { get { return GraphPane.YAxis.Scale.Min; }}
        public double YAxisMax { get { return GraphPane.YAxis.Scale.Max; }}

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
                if (toolStripButtonShowAnnotations.Checked && Program.SkylineOffscreen)
                {
                    var annotationCurves = GraphPane.CurveList.FindAll(c => c is StickItem && c.Tag is SpectrumGraphItem);
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
                    return GraphPane.GraphObjList.OfType<TextObj>()
                        .ToList().FindAll(txt => txt.Location.X >= XAxisMin && txt.Location.X <= XAxisMax)      //select only annotations visible in the current window)
                        .Select(label => label.Text).ToHashSet();
                }
            }
        }

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
