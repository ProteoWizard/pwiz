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
using pwiz.MSGraph;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using ZedGraph;

namespace pwiz.Skyline.Controls.Graphs
{
    public partial class GraphFullScan : DockableFormEx, IGraphContainer
    {
        private const int MIN_DOT_RADIUS = 4;
        private const int MAX_DOT_RADIUS = 13;

        private readonly IDocumentUIContainer _documentContainer;
        private readonly GraphHelper _graphHelper;
        private readonly BackgroundScanProvider _scanProvider;
        private MsDataSpectrum[] _fullScans;
        private HeatMapData _heatMapData;
        private double _maxMz;
        private double _maxIntensity;
        private double _maxDriftTime;
        private string _fileName;
        private int _transitionIndex;
        private int _scanIndex;
        private readonly string[] _sourceNames;
        private ChromSource _source;
        private bool _zoomXAxis;
        private bool _zoomYAxis;

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
            _scanProvider = new BackgroundScanProvider(this, SetScans, HandleLoadScanException);
            _sourceNames = new string[Helpers.CountEnumValues<ChromSource>()];
            _sourceNames[(int) ChromSource.ms1] = Resources.GraphFullScan_GraphFullScan_MS1;
            _sourceNames[(int) ChromSource.fragment] = Resources.GraphFullScan_GraphFullScan_MS_MS;
            _sourceNames[(int) ChromSource.sim] = Resources.GraphFullScan_GraphFullScan_SIM;

            GraphPane.Title.IsVisible = true;
            GraphPane.Legend.IsVisible = false;
            // Make sure to use italics for "m/z"
            AbstractMSGraphItem.SetAxisText(GraphPane.XAxis, Resources.AbstractMSGraphItem_CustomizeXAxis_MZ);

            magnifyBtn.Checked = Settings.Default.AutoZoomFullScanGraph;
            spectrumBtn.Checked = Settings.Default.SumScansFullScan;
            filterBtn.Checked = Settings.Default.FilterDriftTimesFullScan;

            spectrumBtn.Visible = false;
            filterBtn.Visible = false;
            lblScanId.Visible = false;  // you might want to show the scan index for debugging
        }

        private void SetScans(MsDataSpectrum[] scans)
        {
            _fullScans = scans;
            _heatMapData = null;
            if (_fullScans == null)
                return;

            // Find max values.
            _maxMz = 0;
            _maxIntensity = 0;
            GetMaxMzIntensity(out _maxMz, out _maxIntensity);
            _maxDriftTime = 0;
            foreach (var scan in scans)
                _maxDriftTime = Math.Max(_maxDriftTime, scan.DriftTimeMsec ?? 0);

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

        private ChromSource SourceFromName(string name)
        {
            return (ChromSource) _sourceNames.IndexOf(e => e == name);
        }

        private string NameFromSource(ChromSource source)
        {
            return _sourceNames[(int) source];
        }

        public void ShowSpectrum(IScanProvider scanProvider, int transitionIndex, int scanIndex)
        {
            _scanProvider.SetScanProvider(scanProvider);

            if (scanProvider != null)
            {
                _source = _scanProvider.Source;
                _transitionIndex = transitionIndex;
                _scanIndex = scanIndex;
                _fileName = scanProvider.DataFilePath.GetFileName();

                comboBoxScanType.Items.Clear();
                foreach (var source in new[] { ChromSource.ms1, ChromSource.fragment, ChromSource.sim })
                {
                    foreach (var transition in _scanProvider.Transitions)
                    {
                        if (transition.Source == source)
                        {
                            comboBoxScanType.Items.Add(NameFromSource(source));
                            break;
                        }
                    }
                }
                comboBoxScanType.SelectedIndexChanged -= comboBoxScanType_SelectedIndexChanged;
                comboBoxScanType.SelectedItem = NameFromSource(_source);
                comboBoxScanType.SelectedIndexChanged += comboBoxScanType_SelectedIndexChanged;
                comboBoxScanType.Enabled = true;

                LoadScan(true, true);
            }
            else
            {
                _fullScans = null;
                _fileName = null;
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

            int scanId = GetScanId();
            if (scanId < 0)
            {
                _fullScans = null;
                GraphPane.CurveList.Clear();
                GraphPane.GraphObjList.Clear();
                ClearGraph();
                FireSelectedScanChanged(0);
                return;
            }

            // Display scan id as 1-based to match SeeMS.
            lblScanId.Text = (scanId+1).ToString("D"); // Not L10N

            RunBackground(LoadingTextIfNoChange);
            _scanProvider.GetScans(scanId);
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
            var fullScans = _fullScans;
            Thread.Sleep(200);
            if (ReferenceEquals(fullScans, _fullScans))
            {
                Invoke(new Action(() =>
                {
                    // Need to check again once on the UI thread
                    if (ReferenceEquals(fullScans, _fullScans))
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
            if (_fullScans == null)
                return;

            GraphPane.CurveList.Clear();
            GraphPane.GraphObjList.Clear();

            bool hasDriftDimension = _fullScans.Length > 1;
            bool useHeatMap = hasDriftDimension && !Settings.Default.SumScansFullScan;

            filterBtn.Visible = spectrumBtn.Visible = hasDriftDimension;
            graphControl.IsEnableVPan = graphControl.IsEnableVZoom = useHeatMap;
            GraphPane.Legend.IsVisible = useHeatMap;

            if (hasDriftDimension)
            {
                // Is there actually any drift time filtering available?
                double minDriftTime, maxDriftTime;
                GetDriftRange(out minDriftTime, out maxDriftTime, ChromSource.unknown); // Get range of drift times for all products and precursors
                if ((minDriftTime == double.MinValue) && (maxDriftTime == double.MaxValue))
                {
                    filterBtn.Visible = false;
                    filterBtn.Checked = false;
                }
            }

            if (useHeatMap)
                CreateDriftTimeHeatmap();
            else
                CreateSingleScan();

            // Add extraction boxes.
            for (int i = 0; i < _scanProvider.Transitions.Length; i++)
            {
                var transition = _scanProvider.Transitions[i];
                if (transition.Source != _source)
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
            for (int i = 0; i < _scanProvider.Transitions.Length; i++)
            {
                var transition = _scanProvider.Transitions[i];
                if (transition.Source != _source)
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

            double retentionTime = _fullScans[0].RetentionTime ?? _scanProvider.Times[_scanIndex];
            GraphPane.Title.Text = string.Format(Resources.GraphFullScan_CreateGraph__0_____1_F2__min_, _fileName, retentionTime);

            FireSelectedScanChanged(retentionTime);
        }

        /// <summary>
        /// Create a drift time heat map graph.
        /// </summary>
        private void CreateDriftTimeHeatmap()
        {
            GraphPane.YAxis.Title.Text = Resources.GraphFullScan_CreateDriftTimeHeatmap_Drift_Time__ms_;
            graphControl.IsEnableVZoom = graphControl.IsEnableVPan = true;

            if (_heatMapData == null)
            {
                var points = new List<Point3D>(5000);
                foreach (var scan in _fullScans)
                {
                    if (!scan.DriftTimeMsec.HasValue)
                        continue;
                    for (int j = 0; j < scan.Mzs.Length; j++)
                        points.Add(new Point3D(scan.Mzs[j], scan.DriftTimeMsec.Value, scan.Intensities[j]));
                }
                _heatMapData = new HeatMapData(points);
            }

            double minDrift;
            double maxDrift;
            GetDriftRange(out minDrift, out maxDrift, _source);  // There may be a different drift time filter for products in Waters

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

            if (!Settings.Default.FilterDriftTimesFullScan)
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
            var pointLists = new PointPairList[_scanProvider.Transitions.Length];
            for (int i = 0; i < pointLists.Length; i++)
                pointLists[i] = new PointPairList();
            var defaultPointList = new PointPairList();
            var allPointList = new PointPairList();

            // Assign each point to a transition point list, or else the default point list.
            IList<double> mzs;
            IList<double> intensities;
            if (_fullScans.Length == 1)
            {
                mzs = _fullScans[0].Mzs;
                intensities = _fullScans[0].Intensities;
            }
            else
            {
                mzs = new List<double>();
                intensities = new List<double>();

                var fullScans = GetFilteredScans();

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
                for (int j = 0; j < _scanProvider.Transitions.Length; j++)
                {
                    var transition = _scanProvider.Transitions[j];
                    if (transition.Source != _source ||
                        mz <= transition.ProductMz - transition.ExtractionWidth/2 ||
                        mz > transition.ProductMz + transition.ExtractionWidth/2)
                        continue;
                    assignedPointList = pointLists[j];
                    break;
                }
                assignedPointList.Add(mz, intensity);
            }

            // Create a graph item for each point list with its own color.
            for (int i = 0; i < pointLists.Length; i++)
            {
                var transition = _scanProvider.Transitions[i];
                if (transition.Source != _source)
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
            if (_fullScans.Length > 0 && !_fullScans[0].Centroided)
            {
                var item = new SpectrumShadeItem(allPointList, Color.FromArgb(100, 225, 225, 150));
                var curveItem = _graphHelper.GraphControl.AddGraphItem(GraphPane, item, false);
                curveItem.Label.IsVisible = false;
            }

            GraphPane.SetScale(CreateGraphics());
        }

        private MsDataSpectrum[] GetFilteredScans()
        {
            var fullScans = _fullScans;
            double minDrift, maxDrift;
            GetDriftRange(out minDrift, out maxDrift, _source);
            if (Settings.Default.FilterDriftTimesFullScan)
                fullScans = fullScans.Where(s => minDrift <= s.DriftTimeMsec && s.DriftTimeMsec <= maxDrift).ToArray();
            return fullScans;
        }

        private void GetDriftRange(out double minDrift, out double maxDrift, ChromSource sourceType)
        {
            minDrift = double.MaxValue;
            maxDrift = double.MinValue;
            foreach (var transition in _scanProvider.Transitions)
            {
                if (!transition.IonMobilityValue.HasValue || !transition.IonMobilityExtractionWidth.HasValue)
                {
                    // Accept all values
                    minDrift = double.MinValue;
                    maxDrift = double.MaxValue;
                }
                else if (sourceType == ChromSource.unknown || transition.Source == sourceType)
                {
                    // Products and precursors may have different expected drift time values in Waters MsE
                    double startDrift = transition.IonMobilityValue.Value -
                                        transition.IonMobilityExtractionWidth.Value / 2;
                    double endDrift = startDrift + transition.IonMobilityExtractionWidth.Value;
                    minDrift = Math.Min(minDrift, startDrift);
                    maxDrift = Math.Max(maxDrift, endDrift);
                }
            }
        }

        private static double FindMinMz(MsDataSpectrum[] spectra, int[] indices)
        {
            double minMz = double.MaxValue;
            for (int i = 0; i < indices.Length; i++)
            {
                var scan = spectra[i];
                int indexMz = indices[i];
                if (indexMz != -1 && indexMz < scan.Mzs.Length)
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
                int indexMz = indices[i];
                // In case of zero length m/z arrays, set index to -1 the first time through
                if (indexMz >= scan.Mzs.Length)
                    indexMz = indices[i] = -1;
                if (indexMz == -1 || mz != scan.Mzs[indexMz])
                    continue;
                intensity += scan.Intensities[indexMz++];
                indices[i] = indexMz < scan.Mzs.Length ? indexMz : -1;
            }
            return intensity;
        }

        private void GetMaxMzIntensity(out double maxMz, out double maxIntensity)
        {
            var fullScans = GetFilteredScans();
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
            graphControl.GraphPane.Title.Text = _fileName;
        }

        [Browsable(true)]
        public event EventHandler<SelectedScanEventArgs> SelectedScanChanged;

        public void FireSelectedScanChanged(double retentionTime)
        {
            IsLoaded = true;
            if (SelectedScanChanged != null)
            {
                if (_fullScans != null)
                    SelectedScanChanged(this, new SelectedScanEventArgs(_scanProvider.DataFilePath, retentionTime, _scanProvider.Transitions[_transitionIndex].Id));
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
                _scanProvider.SetScanProvider(null);

                LoadScan(true, true);
            }
        }

        private void GraphFullScan_VisibleChanged(object sender, EventArgs e)
        {
            if (IsHidden)
            {
                _fullScans = null;
                FireSelectedScanChanged(0);
                UpdateUI(false);
            }
        }

        private void ZoomXAxis()
        {
            if (_scanProvider == null || _scanProvider.Transitions.Length == 0)
                return;

            var xScale = GraphPane.XAxis.Scale;
            xScale.MinAuto = xScale.MaxAuto = false;

            if (magnifyBtn.Checked)
            {
                double mz = _source == ChromSource.ms1
                    ? _scanProvider.Transitions[_transitionIndex].PrecursorMz
                    : _scanProvider.Transitions[_transitionIndex].ProductMz;
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
            if (_scanProvider == null || _scanProvider.Transitions.Length == 0)
                return;

            var yScale = GraphPane.YAxis.Scale;
            yScale.MinAuto = yScale.MaxAuto = false;
            bool isSpectrum = !spectrumBtn.Visible || spectrumBtn.Checked;
            GraphPane.LockYAxisAtZero = isSpectrum;
            
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
                yScale.Max = _maxDriftTime * 1.1;
            }
            else
            {
                double minDriftTime, maxDriftTime;
                GetDriftRange(out minDriftTime, out maxDriftTime, _source);
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
                    yScale.Max = _maxDriftTime * 1.1;
                }
            }
            GraphPane.AxisChange();
        }

        public void UpdateUI(bool selectionChanged = true)
        {
            // Only worry about updates, if the graph is visible
            // And make sure it is not disposed, since rendering happens on a timer
            if (!Visible || IsDisposed || _scanProvider == null)
                return;

            if (_fullScans != null)
            {
                leftButton.Enabled = (_scanIndex > 0);
                rightButton.Enabled = (_scanIndex < _scanProvider.Times.Length-1);
                lblScanId.Text = GetScanId().ToString("D"); // Not L10N
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

        private int[][] GetScanIds()
        {
            if (_scanProvider != null)
            {
                foreach (var transition in _scanProvider.Transitions)
                {
                    if (transition.Source == _scanProvider.Source)
                        return transition.ScanIds;
                }
            }
            return null;
        }

        private int GetScanId()
        {
            var scanIds = GetScanIds();
            return scanIds != null ? scanIds[(int)_source][_scanIndex] : -1;
        }

        public void LockYAxis(bool lockY)
        {
            graphControl.IsEnableVPan = graphControl.IsEnableVZoom = !lockY;
            graphControl.Refresh();
        }

        protected override void OnClosed(EventArgs e)
        {
            _documentContainer.UnlistenUI(OnDocumentUIChanged);
            _scanProvider.Dispose();
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
            if (_fullScans == null)
                return;
            if (_scanIndex + delta < 0 || _scanIndex + delta >= _scanProvider.Times.Length)
                return;

            int[][] scanIds = GetScanIds();
            var sourceScanIds = scanIds[(int) _source];
            int scanId = sourceScanIds[_scanIndex];
            while ((delta < 0 && _scanIndex >= 0) || (delta > 0 && _scanIndex < sourceScanIds.Length))
            {
                _scanIndex += delta;
                int newScanId = sourceScanIds[_scanIndex];
                if (newScanId != scanId)
                {
                    if (delta < 0)
                    {
                        // Choose first scan with a particular scanId by continuing backward until another
                        // change in scan ID is found.
                        while (_scanIndex > 0 && sourceScanIds[_scanIndex - 1] == sourceScanIds[_scanIndex])
                            _scanIndex--;
                    }
                    break;
                }
            }

            LoadScan(false, false);
        }

        private void comboBoxScanType_SelectedIndexChanged(object sender, EventArgs e)
        {
            _source = SourceFromName(comboBoxScanType.Text);
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
            Settings.Default.FilterDriftTimesFullScan = filterBtn.Checked = filter;
            _zoomYAxis = true;
            SetScans(_fullScans);
        }

        private void btnIsolationWindow_Click(object sender, EventArgs e)
        {
            var spectrum = _fullScans[0];
            var target = spectrum.Precursors[0].IsolationWindowTargetMz;
            if (!target.HasValue)
                MessageDlg.Show(this, "No isolation target"); // Not L10N
            else
            {
                double low = target.Value - spectrum.Precursors[0].IsolationWindowLower ?? 0;
                double high = target.Value + spectrum.Precursors[0].IsolationWindowUpper ?? 0;
                MessageDlg.Show(this, string.Format("Isolation window: {0}, {1}, {2}", low, target, high)); // Not L10N
            }
        }

        #region Mouse events

        private void graphControl_ContextMenuBuilder(ZedGraphControl sender, ContextMenuStrip menuStrip, Point mousePt, ZedGraphControl.ContextMenuObjectState objState)
        {
            ZedGraphHelper.BuildContextMenu(graphControl, menuStrip, true);
        }

        private void graphControl_MouseClick(object sender, MouseEventArgs e)
        {
            var nearestLabel = GetNearestLabel(new PointF(e.X, e.Y));
            if (nearestLabel == null)
                return;
            _transitionIndex = (int) nearestLabel.Tag;
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

        public double XAxisMin { get { return GraphPane.XAxis.Scale.Min; }}
        public double XAxisMax { get { return GraphPane.XAxis.Scale.Max; }}
        public double YAxisMin { get { return GraphPane.YAxis.Scale.Min; }}
        public double YAxisMax { get { return GraphPane.YAxis.Scale.Max; }}

        public void SelectScanType(ChromSource source)
        {
            comboBoxScanType.SelectedItem = NameFromSource(source);
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


        /// <summary>
        /// Provides a constant background thread with responsibility for all interactions
        /// with <see cref="IScanProvider"/>, necessary because <see cref="MsDataFileImpl"/> objects
        /// must be accessed on the same thread.
        /// </summary>
        private class BackgroundScanProvider : IDisposable
        {
            private const int MAX_CACHE_COUNT = 2;

            private bool _disposing;
            private int _scanIdNext;
            private IScanProvider _scanProvider;
            private readonly List<IScanProvider> _cachedScanProviders;
            private readonly List<IScanProvider> _oldScanProviders;
            // ReSharper disable once PrivateFieldCanBeConvertedToLocalVariable
            private readonly Thread _backgroundThread;

            private readonly Form _form;
            private readonly Action<MsDataSpectrum[]> _successAction;
            private readonly Action<Exception> _failureAction;

            public BackgroundScanProvider(Form form, Action<MsDataSpectrum[]> successAction, Action<Exception> failureAction)
            {
                _scanIdNext = -1;

                _oldScanProviders = new List<IScanProvider>();
                _cachedScanProviders = new List<IScanProvider>();
                _backgroundThread = new Thread(Work) { Name = GetType().Name, Priority = ThreadPriority.BelowNormal };
                _backgroundThread.Start();

                _form = form;
                _successAction = successAction;
                _failureAction = failureAction;
            }

            public MsDataFileUri DataFilePath
            {
                get { return GetProviderProperty(p => p.DataFilePath, new MsDataFilePath(string.Empty)); }
            }

            public ChromSource Source
            {
                get { return GetProviderProperty(p => p.Source, ChromSource.unknown); }
            }

            public TransitionFullScanInfo[] Transitions
            {
                get { return GetProviderProperty(p => p.Transitions, new TransitionFullScanInfo[0]); }
            }

            public float[] Times
            {
                get { return GetProviderProperty(p => p.Times, new float[0]); }
            }

            private TProp GetProviderProperty<TProp>(Func<IScanProvider, TProp> getProp, TProp defaultValue)
            {
                lock (this)
                {
                    return _scanProvider != null ? getProp(_scanProvider) : defaultValue;
                }
            }

            /// <summary>
            /// Always run on a specific background thread to avoid changing threads when dealing
            /// with a scan provider, which can mess up data readers used by ProteoWizard.
            /// </summary>
            private void Work()
            {
                while (!_disposing)
                {
                    IScanProvider scanProvider;
                    int scanId;

                    lock (this)
                    {
                        while ((_scanProvider == null || _scanIdNext == -1) && _oldScanProviders.Count == 0)
                            Monitor.Wait(this);

                        scanProvider = _scanProvider;
                        scanId = _scanIdNext;
                        _scanIdNext = -1;
                    }

                    if (scanProvider != null && scanId != -1)
                    {
                        try
                        {
                            var fullScans = scanProvider.GetScans(scanId);
                            _form.Invoke(new Action(() => _successAction(fullScans)));
                        }
                        catch (Exception ex)
                        {
                            _form.Invoke(new Action(() => _failureAction(ex)));
                        }
                    }

                    DisposeAllProviders();
                }

                lock (this)
                {
                    DisposeAllProviders();

                    Monitor.PulseAll(this);
                }
            }

            public void SetScanProvider(IScanProvider newScanProvider)
            {
                lock (this)
                {
                    if (_scanProvider != null)
                    {
                        _cachedScanProviders.Insert(0, _scanProvider);

                        if (newScanProvider != null)
                        {
                            AdoptCachedProvider(newScanProvider);
                        }

                        // Queue for disposal
                        if (_cachedScanProviders.Count > MAX_CACHE_COUNT)
                        {
                            _oldScanProviders.Add(_cachedScanProviders[MAX_CACHE_COUNT]);
                            _cachedScanProviders.RemoveAt(MAX_CACHE_COUNT);
                        }
                    }
                    _scanProvider = newScanProvider;
                    if (newScanProvider == null)
                    {
                        _oldScanProviders.AddRange(_cachedScanProviders);
                        _cachedScanProviders.Clear();
                    }
                    Monitor.PulseAll(this);
                }
            }

            private void AdoptCachedProvider(IScanProvider scanProvider)
            {
                lock (this)
                {
                    for (int i = 0; i < _cachedScanProviders.Count; i++)
                    {
                        if (scanProvider.Adopt(_cachedScanProviders[i]))
                        {
                            _oldScanProviders.Add(_cachedScanProviders[i]);
                            _cachedScanProviders.RemoveAt(i);
                            return;
                        }
                    }
                }
            }

            public void GetScans(int scanId)
            {
                lock (this)
                {
                    _scanIdNext = scanId;

                    if (_scanIdNext != -1)
                        Monitor.PulseAll(this);
                }
            }

            private void DisposeAllProviders()
            {
                IScanProvider[] disposeScanProviders;
                lock (this)
                {
                    disposeScanProviders = _oldScanProviders.ToArray();
                    _oldScanProviders.Clear();
                }

                foreach (var provider in disposeScanProviders)
                    provider.Dispose();
            }

            public void Dispose()
            {
                // Wait for dispose to happen on the background thread
                lock (this)
                {
                    _disposing = true;
                    SetScanProvider(null);
                }
            }
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