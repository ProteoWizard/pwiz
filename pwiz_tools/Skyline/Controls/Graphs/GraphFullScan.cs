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
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using pwiz.Common.Chemistry;
using pwiz.MSGraph;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using ZedGraph;

namespace pwiz.Skyline.Controls.Graphs
{
    public partial class GraphFullScan : DockableFormEx, IGraphContainer
    {
        private const int MIN_DOT_RADIUS = 4;
        private const int MAX_DOT_RADIUS = 13;

        private readonly IDocumentUIContainer _documentContainer;
        private readonly GraphHelper _graphHelper;
        private HeatMapData _heatMapData;
        private double _maxMz;
        private double _maxIntensity;
        private double _maxIonMobility;
        private bool _zoomXAxis;
        private bool _zoomYAxis;
        private readonly MsDataFileScanHelper _msDataFileScanHelper;

        public GraphFullScan(IDocumentUIContainer documentUIContainer)
        {
            InitializeComponent();

            graphControl.GraphPane = new HeatMapGraphPane
            {
                MinDotRadius = MIN_DOT_RADIUS,
                MaxDotRadius = MAX_DOT_RADIUS
            };

            Icon = Resources.SkylineData;
            _graphHelper = GraphHelper.Attach(graphControl);
            _documentContainer = documentUIContainer;
            _documentContainer.ListenUI(OnDocumentUIChanged);

            _msDataFileScanHelper = new MsDataFileScanHelper(SetSpectra, HandleLoadScanException, false); // We need zero intensity points for proper display

            GraphPane.Title.IsVisible = true;
            GraphPane.Legend.IsVisible = false;
            // Make sure to use italics for "m/z"
            AbstractMSGraphItem.SetAxisText(GraphPane.XAxis, Resources.AbstractMSGraphItem_CustomizeXAxis_MZ);

            magnifyBtn.Checked = Settings.Default.AutoZoomFullScanGraph;
            spectrumBtn.Checked = Settings.Default.SumScansFullScan;
            filterBtn.Checked = Settings.Default.FilterIonMobilityFullScan;

            spectrumBtn.Visible = false;
            filterBtn.Visible = false;
            lblScanId.Visible = false;  // you might want to show the scan index for debugging
        }

        private void SetSpectra(MsDataSpectrum[] spectra)
        {
            Invoke(new Action(() => SetSpectraUI(spectra)));
        }

        private void SetSpectraUI(MsDataSpectrum[] spectra)
        {
            _msDataFileScanHelper.MsDataSpectra = spectra;
            _heatMapData = null;
            if (_msDataFileScanHelper.MsDataSpectra == null)
                return;

            // Find max values.
            _maxMz = 0;
            _maxIntensity = 0;
            GetMaxMzIntensity(out _maxMz, out _maxIntensity);
            GetMaxMobility(out _maxIonMobility);

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
            CreateGraph();
            UpdateUI();
        }

        private void HandleLoadScanException(Exception ex)
        {
            Invoke(new Action(() => HandleLoadScanExceptionUI(ex)));
        }

        private void HandleLoadScanExceptionUI(Exception ex)
        {
            GraphPane.Title.Text = Resources.GraphFullScan_LoadScan_Spectrum_unavailable;
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

        public void ShowSpectrum(IScanProvider scanProvider, int transitionIndex, int scanIndex)
        {
            _msDataFileScanHelper.UpdateScanProvider(scanProvider, transitionIndex, scanIndex);

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
            _msDataFileScanHelper.ScanProvider.SetScanForBackgroundLoad(scanId);
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
                        GraphPane.Title.Text = Resources.GraphFullScan_LoadScan_Loading___;
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

            bool hasIonMobilityDimension = _msDataFileScanHelper.MsDataSpectra.Length > 1 ||
                                           _msDataFileScanHelper.MsDataSpectra.First().IonMobilities != null;
            bool useHeatMap = hasIonMobilityDimension && !Settings.Default.SumScansFullScan;

            filterBtn.Visible = spectrumBtn.Visible = hasIonMobilityDimension;
            graphControl.IsEnableVPan = graphControl.IsEnableVZoom = useHeatMap;
            GraphPane.Legend.IsVisible = useHeatMap;

            if (hasIonMobilityDimension)
            {
                // Is there actually any drift time filtering available?
                double minIonMobility, maxIonMobility;
                _msDataFileScanHelper.GetIonMobilityRange(out minIonMobility, out maxIonMobility, ChromSource.unknown); // Get range of IM values for all products and precursors
                if ((minIonMobility == double.MinValue) && (maxIonMobility == double.MaxValue))
                {
                    filterBtn.Visible = false;
                    filterBtn.Checked = false;
                }
            }

            if (useHeatMap)
            {
                ZoomYAxis(); // Call this again now that cues are there to indicate need for drift scale
                CreateIonMobilityHeatmap();
            }
            else
            {
                CreateSingleScan();
            }

            // Add extraction boxes.
            for (int i = 0; i < _msDataFileScanHelper.ScanProvider.Transitions.Length; i++)
            {
                var transition = _msDataFileScanHelper.ScanProvider.Transitions[i];
                if (transition.Source != _msDataFileScanHelper.Source)
                    continue;
                var color1 = Blend(transition.Color, Color.White, 0.60);
                var color2 = Blend(transition.Color, Color.White, 0.95);
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
            for (int i = 0; i < _msDataFileScanHelper.ScanProvider.Transitions.Length; i++)
            {
                var transition = _msDataFileScanHelper.ScanProvider.Transitions[i];
                if (transition.Source != _msDataFileScanHelper.Source)
                    continue;
                var label = new TextObj(transition.Name, transition.ProductMz, 0.02, CoordType.XScaleYChartFraction, AlignH.Center, AlignV.Top)
                {
                    ZOrder = ZOrder.D_BehindAxis,
                    IsClippedToChartRect = true,
                    Tag = i
                };
                label.FontSpec.Border.IsVisible = false;
                label.FontSpec.FontColor = Blend(transition.Color, Color.Black, 0.30);
                label.FontSpec.IsBold = true;
                label.FontSpec.Fill = new Fill(Color.FromArgb(180, Color.White));
                GraphPane.GraphObjList.Add(label);
            }

            double retentionTime = _msDataFileScanHelper.MsDataSpectra[0].RetentionTime ?? _msDataFileScanHelper.ScanProvider.Times[_msDataFileScanHelper.ScanIndex];
            GraphPane.Title.Text = string.Format(Resources.GraphFullScan_CreateGraph__0_____1_F2__min_, _msDataFileScanHelper.FileName, retentionTime);
            if (Settings.Default.ShowFullScanNumber && _msDataFileScanHelper.MsDataSpectra.Any())
            {
                if (_msDataFileScanHelper.MsDataSpectra.Length > 1) // For 2-array ion mobility, show the overall range
                {
                    GraphPane.Title.Text = TextUtil.SpaceSeparate(GraphPane.Title.Text,
                        Resources.GraphFullScan_CreateGraph_IM_Scan_Range_, _msDataFileScanHelper.MsDataSpectra[0].Id, @"-", _msDataFileScanHelper.MsDataSpectra.Last().Id); 
                }
                else
                {
                    var parts = _msDataFileScanHelper.MsDataSpectra[0].Id.Split('.'); // Check for merge.frame.start.stop from 3-array IMS data
                    var id = parts.Length < 4
                        ? _msDataFileScanHelper.MsDataSpectra[0].Id
                        : string.Format(@"{0}.{1}-{0}.{2}", parts[1], parts[2], parts[3]);
                    GraphPane.Title.Text = TextUtil.SpaceSeparate(GraphPane.Title.Text,
                        Resources.GraphFullScan_CreateGraph_Scan_Number_, id);
                }
            }

            FireSelectedScanChanged(retentionTime);
        }

        /// <summary>
        /// Create an ion mobility heat map graph.
        /// </summary>
        private void CreateIonMobilityHeatmap()
        {
            GraphPane.YAxis.Title.Text = IonMobilityFilter.IonMobilityUnitsL10NString(_msDataFileScanHelper.IonMobilityUnits);
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

                        points.Add(new Point3D(scan.Mzs[j], mobilityValue, scan.Intensities[j]));
                    }
                }
                _heatMapData = new HeatMapData(points);
            }

            double minDrift;
            double maxDrift;
            _msDataFileScanHelper.GetIonMobilityRange(out minDrift, out maxDrift, _msDataFileScanHelper.Source);  // There may be a different drift time filter for products in Waters

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

        /// <summary>
        /// Create stick graph of a single scan.
        /// </summary>
        private void CreateSingleScan()
        {
            GraphPane.YAxis.Title.Text = Resources.AbstractMSGraphItem_CustomizeYAxis_Intensity;
            graphControl.IsEnableVZoom = graphControl.IsEnableVPan = false;

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

                var fullScans = _msDataFileScanHelper.GetFilteredScans();
                negativeScan = fullScans.Any() && fullScans.First().NegativeCharge;

                double minMz;
                var indices = new int[fullScans.Length];
                while ((minMz = FindMinMz(fullScans, indices)) < double.MaxValue)
                {
                    mzs.Add(minMz);
                    intensities.Add(SumIntensities(fullScans, minMz, indices));
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
                        mz <= transition.ProductMz.Value - transition.ExtractionWidth/2 ||
                        mz > transition.ProductMz.Value + transition.ExtractionWidth/2)
                        continue;
                    assignedPointList = pointLists[j];
                    break;
                }
                assignedPointList.Add(mz, intensity);
            }

            // Create a graph item for each point list with its own color.
            for (int i = 0; i < pointLists.Length; i++)
            {
                var transition = _msDataFileScanHelper.ScanProvider.Transitions[i];
                if (transition.Source != _msDataFileScanHelper.Source)
                    continue;
                var item = new SpectrumItem(pointLists[i], transition.Color, 2);
                var curveItem = _graphHelper.GraphControl.AddGraphItem(GraphPane, item, false);
                curveItem.Label.IsVisible = false;
            }

            // Add points that aren't associated with a transition.
            {
                var item = new SpectrumItem(defaultPointList, Color.Gray);
                var curveItem = _graphHelper.GraphControl.AddGraphItem(GraphPane, item, false);
                curveItem.Label.IsVisible = false;
            }

            // Create curve for all points to provide shading behind stick graph.
            if (_msDataFileScanHelper.MsDataSpectra.Length > 0 && !_msDataFileScanHelper.MsDataSpectra[0].Centroided)
            {
                var item = new SpectrumShadeItem(allPointList, Color.FromArgb(100, 225, 225, 150));
                var curveItem = _graphHelper.GraphControl.AddGraphItem(GraphPane, item, false);
                curveItem.Label.IsVisible = false;
            }

            GraphPane.SetScale(CreateGraphics());
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

        private static double SumIntensities(MsDataSpectrum[] spectra, double mz, int[] indices)
        {
            double intensity = 0;
            for (int i = 0; i < indices.Length; i++)
            {
                var scan = spectra[i];
                int indexMz;
                // Sometimes spectra have multiple intensities for a given m/z.  Sum all intensities for that m/z
                for (indexMz = indices[i]; indexMz < scan.Mzs.Length && scan.Mzs[indexMz] == mz; indexMz++)
                {
                    intensity += scan.Intensities[indexMz];
                }
                indices[i] = indexMz;
            }
            return intensity;
        }

        private void GetMaxMzIntensity(out double maxMz, out double maxIntensity)
        {
            var fullScans = _msDataFileScanHelper.GetFilteredScans();
            maxMz = 0;
            maxIntensity = 0;

            double minMz;
            var indices = new int[fullScans.Length];
            while ((minMz = FindMinMz(fullScans, indices)) < double.MaxValue)
            {
                maxMz = Math.Max(maxMz, minMz);
                double intensity = SumIntensities(fullScans, minMz, indices);
                maxIntensity = Math.Max(maxIntensity, intensity);
            }
        }

        private void GetMaxMobility(out double maxIonMobility)
        {
            maxIonMobility = 0;
            foreach (var spectrum in _msDataFileScanHelper.MsDataSpectra)
            {
                var spectrumMaxMobility = Math.Abs(spectrum.MaxIonMobility ?? spectrum.IonMobility.Mobility ?? 0);
                maxIonMobility = Math.Max(_maxIonMobility, spectrumMaxMobility);
            }
        }

        private Color Blend(Color baseColor, Color blendColor, double blendAmount)
        {
            return Color.FromArgb(
                (int) (baseColor.R*(1 - blendAmount) + blendColor.R*blendAmount),
                (int) (baseColor.G*(1 - blendAmount) + blendColor.G*blendAmount),
                (int) (baseColor.B*(1 - blendAmount) + blendColor.B*blendAmount));
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
            if (SelectedScanChanged != null)
            {
                if (_msDataFileScanHelper.MsDataSpectra != null)
                    SelectedScanChanged(this, new SelectedScanEventArgs(_msDataFileScanHelper.ScanProvider.DataFilePath, retentionTime, _msDataFileScanHelper.ScanProvider.Transitions[_msDataFileScanHelper.TransitionIndex].Id));
                else
                    SelectedScanChanged(this, new SelectedScanEventArgs(null, 0, null));
            }
        }

        public bool IsLoaded { get; private set; }

        public void OnDocumentUIChanged(object sender, DocumentChangedEventArgs e)
        {
            // If document changed, close file.
            if (e.DocumentPrevious == null || !ReferenceEquals(DocumentUI.Id, e.DocumentPrevious.Id))
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
                xScale.Min = 0;
                xScale.Max = _maxMz * 1.1;
            }
        }

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
                yScale.Min = 0;
                yScale.Max = _maxIonMobility * 1.1;
            }
            else
            {
                double minDriftTime, maxDriftTime;
                _msDataFileScanHelper.GetIonMobilityRange(out minDriftTime, out maxDriftTime, _msDataFileScanHelper.Source);
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
            if (_msDataFileScanHelper.MsDataSpectra != null)
            {
                leftButton.Enabled = (_msDataFileScanHelper.ScanIndex > 0);
                rightButton.Enabled = (_msDataFileScanHelper.ScanIndex < _msDataFileScanHelper.ScanProvider.Times.Count-1);
                lblScanId.Text = _msDataFileScanHelper.GetScanIndex().ToString(@"D");
                if (!spectrumBtn.Checked)
                    GraphPane.SetScale(CreateGraphics());
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

            var sourceScanIds = _msDataFileScanHelper.GetScanIndexes(_msDataFileScanHelper.Source);
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

            LoadScan(false, false);
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
            CreateGraph();
            UpdateUI();
        }

        private void spectrumBtn_CheckedChanged(object sender, EventArgs e)
        {
            HeatMapGraphPane.ShowHeatMap = !spectrumBtn.Checked;
            Settings.Default.SumScansFullScan = spectrumBtn.Checked;
            CreateGraph();
            ZoomYAxis();
            UpdateUI();
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

        #region Mouse events

        private void graphControl_ContextMenuBuilder(ZedGraphControl sender, ContextMenuStrip menuStrip, Point mousePt, ZedGraphControl.ContextMenuObjectState objState)
        {
            ZedGraphHelper.BuildContextMenu(graphControl, menuStrip, true);
            showScanNumberContextMenuItem.Checked = Settings.Default.ShowFullScanNumber;
            menuStrip.Items.Add(new ToolStripSeparator());
            menuStrip.Items.Add(showScanNumberContextMenuItem);
        }

        private void graphControl_MouseClick(object sender, MouseEventArgs e)
        {
            var nearestLabel = GetNearestLabel(new PointF(e.X, e.Y));
            if (nearestLabel == null)
                return;
            _msDataFileScanHelper.TransitionIndex = (int) nearestLabel.Tag;
            magnifyBtn.Checked = true;
            CreateGraph();
            ZoomXAxis();
            ZoomYAxis();
            UpdateUI();
        }

        private bool graphControl_MouseMove(ZedGraphControl sender, MouseEventArgs e)
        {
            var pt = new PointF(e.X, e.Y);
            var nearestLabel = GetNearestLabel(pt);
            if (nearestLabel == null)
                return false;
            
            graphControl.Cursor = Cursors.Hand;
            return true;
        }

        private TextObj GetNearestLabel(PointF mousePoint)
        {
            using (Graphics g = CreateGraphics())
            {
                object nearestObject;
                int index;
                if (GraphPane.FindNearestObject(mousePoint, g, out nearestObject, out index))
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

        #endregion Test support

        private void showScanNumberToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.ShowFullScanNumber = !Settings.Default.ShowFullScanNumber;
            CreateGraph();
            UpdateUI();
        }
    }

    public class SpectrumItem : AbstractMSGraphItem
    {
        private readonly IPointList _points;
        private readonly Color _color;

        public SpectrumItem(IPointList points, Color color, float width = 1)
        {
            _points = points;
            _color = color;
            LineWidth = Settings.Default.SpectrumLineWidth*width;
        }

        public override string Title { get { return null; } }

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
        public SpectrumShadeItem(IPointList points, Color color)
            : base(points, color)
        {
        }

        public override MSGraphItemDrawMethod GraphItemDrawMethod
        {
            get { return MSGraphItemDrawMethod.fill; }
        }

    }

    public sealed class SelectedScanEventArgs : EventArgs
    {
        public SelectedScanEventArgs(MsDataFileUri dataFile, double retentionTime, Identity transitionId)
        {
            DataFile = dataFile;
            RetentionTime = retentionTime;
            TransitionId = transitionId;
        }

        public MsDataFileUri DataFile { get; private set; }
        public double RetentionTime { get; private set; }
        public Identity TransitionId { get; private set; }
    }
}
