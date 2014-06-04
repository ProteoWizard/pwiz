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
        private const float DRIFT_SAMPLE_RADIUS = 10.0f;

        private readonly IDocumentUIContainer _documentContainer;
        private readonly GraphHelper _graphHelper;
        private readonly BackgroundScanProvider _scanProvider;
        private MsDataSpectrum[] _fullScans;
        private string _fileName;
        private int _transitionIndex;
        private int _scanIndex;
        private readonly string[] _sourceNames;
        private ChromSource _source;

        public GraphFullScan(IDocumentUIContainer documentUIContainer)
        {
            InitializeComponent();

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
            GraphPane.XAxis.Title.Text = Resources.AbstractMSGraphItem_CustomizeXAxis_MZ;

            magnifyBtn.Checked = Settings.Default.AutoZoomFullScanGraph;
        }

        private void SetScans(MsDataSpectrum[] scans)
        {
            _fullScans = scans;
            CreateGraph();
        }

        private void HandleLoadScanException(Exception ex)
        {
            GraphPane.Title.Text = Resources.GraphFullScan_LoadScan_Spectrum_unavailable;
            MessageDlg.Show(this, ex.Message);
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

                Zoom();
                LoadScan();
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
            graphControl.Focus();
        }

        private void LoadScan()
        {
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
            GraphPane.CurveList.Clear();
            GraphPane.GraphObjList.Clear();

            if (_fullScans == null)
                return;

            bool hasDriftDimension = _fullScans.Length > 1;
            bool useHeatMap = hasDriftDimension && !Settings.Default.SumScansFullScan;

            filterBtn.Visible = spectrumBtn.Visible = hasDriftDimension;
            graphControl.IsEnableVPan = graphControl.IsEnableVZoom = useHeatMap;
            GraphPane.Legend.IsVisible = useHeatMap;

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
                };
                label.FontSpec.Border.IsVisible = false;
                label.FontSpec.FontColor = Blend(transition.Color, Color.Black, 0.30);
                label.FontSpec.IsBold = true;
                label.FontSpec.Fill = new Fill(Color.FromArgb(180, Color.White));
                GraphPane.GraphObjList.Add(label);
            }

            double retentionTime = _fullScans[0].RetentionTime ?? _scanProvider.Times[_scanIndex];
            GraphPane.Title.Text = string.Format("{0} ({1:F2})", _fileName, retentionTime); // Not L10N
            UpdateUI();

            FireSelectedScanChanged(retentionTime);
        }

        /// <summary>
        /// Create a drift time heat map graph.
        /// </summary>
        private void CreateDriftTimeHeatmap()
        {
            GraphPane.YAxis.Title.Text = Resources.GraphFullScan_CreateDriftTimeHeatmap_Drift_Time__ms_;
            graphControl.IsEnableVZoom = graphControl.IsEnableVPan = true;

            var fullScans = GetFilteredScans();

            // Find the maximum intensity.
            double max = double.MinValue;
            for (int i = 0; i < fullScans.Length; i++)
            {
                var scan = fullScans[i];
                for (int j = 0; j < scan.Mzs.Length; j++)
                {
                    double intensity = scan.Intensities[j];
                    max = Math.Max(max, intensity);
                }
            }
            if (max <= 0)
                return;
            double scale = (_heatMapColors.Length / 3 - 1) / Math.Log(max);

            // Create curves for each intensity color.
            var curves = new LineItem[_heatMapColors.Length/3];
            for (int i = 0; i < curves.Length; i++)
            {
                var color = Color.FromArgb(_heatMapColors[i*3], _heatMapColors[i*3 + 1], _heatMapColors[i*3 + 2]);
                curves[i] = new LineItem(string.Empty)
                {
                    Line = new Line {IsVisible = false},
                    Symbol = new Symbol
                    {
                        Border = new Border { IsVisible = false }, 
                        Size = DRIFT_SAMPLE_RADIUS, 
                        Fill = new Fill(color), 
                        Type = SymbolType.Circle
                    }
                };
                if ((i + 1)%(_heatMapColors.Length/3/4) == 0)
                {
                    double intensity = Math.Pow(Math.E, i/scale);
                    curves[i].Label.Text = intensity.ToString("F0"); // Not L10N
                }
                GraphPane.CurveList.Insert(0, curves[i]);
            }

            // Place each point in the proper intensity/color bin.
            for (int i = 0; i < fullScans.Length; i++)
            {
                var scan = fullScans[i];
                for (int j = 0; j < scan.Mzs.Length; j++)
                {
                    if (scan.Intensities[j] > 0)
                    {
                        // A log scale produces a better visual display.
                        int intensity = (int)(Math.Log(scan.Intensities[j]) * scale);
                        if (intensity > 0)
                            curves[intensity].AddPoint(scan.Mzs[j], scan.DriftTimeMsec ?? 0);
                    }
                }
            }

            double minDrift, maxDrift;
            GetDriftRange(out minDrift, out maxDrift);

            var driftTimeBox = new BoxObj(
                0.0,
                maxDrift,
                1.0,
                maxDrift - minDrift,
                Color.Transparent,
                Color.FromArgb(50, Color.Gray))
            {
                Location = { CoordinateFrame = CoordType.XChartFractionYScale },
                ZOrder = ZOrder.F_BehindGrid,
                IsClippedToChartRect = true,
            };
            GraphPane.GraphObjList.Add(driftTimeBox);
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
                var curveItem = _graphHelper.GraphControl.AddGraphItem(GraphPane, item);
                curveItem.Label.IsVisible = false;
            }

            // Add points that aren't associated with a transition.
            {
                var item = new SpectrumItem(defaultPointList, Color.Gray);
                var curveItem = _graphHelper.GraphControl.AddGraphItem(GraphPane, item);
                curveItem.Label.IsVisible = false;
            }
        }

        private MsDataSpectrum[] GetFilteredScans()
        {
            var fullScans = _fullScans;
            double minDrift, maxDrift;
            GetDriftRange(out minDrift, out maxDrift);
            if (Settings.Default.FilterDriftTimesFullScan)
                fullScans = fullScans.Where(s => minDrift <= s.DriftTimeMsec && s.DriftTimeMsec <= maxDrift).ToArray();
            return fullScans;
        }

        private void GetDriftRange(out double minDrift, out double maxDrift)
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
                else
                {
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
            if (SelectedScanChanged != null)
            {
                if (_fullScans != null)
                    SelectedScanChanged(this, new SelectedScanEventArgs(_scanProvider.DataFilePath, retentionTime));
                else
                    SelectedScanChanged(this, new SelectedScanEventArgs(null, 0));
            }
        }

        public void OnDocumentUIChanged(object sender, DocumentChangedEventArgs e)
        {
            // If document changed, close file.
            if (e.DocumentPrevious == null || !ReferenceEquals(DocumentUI.Id, e.DocumentPrevious.Id))
            {
                _scanProvider.SetScanProvider(null);

                LoadScan();
            }
        }

        private void GraphFullScan_VisibleChanged(object sender, EventArgs e)
        {
            if (IsHidden)
                _fullScans = null;
            FireSelectedScanChanged(0);
            UpdateUI(false);
        }

        private void Zoom()
        {
            var xScale = GraphPane.XAxis.Scale;
            var yScale = GraphPane.YAxis.Scale;
            xScale.MinAuto = xScale.MaxAuto = true;
            yScale.MinAuto = yScale.MaxAuto = true;
            GraphPane.LockYAxisAtZero = spectrumBtn.Checked;
            if (magnifyBtn.Checked)
            {
                xScale.MinAuto = xScale.MaxAuto = false;
                double mz = _scanProvider.Source == ChromSource.ms1
                    ? _scanProvider.Transitions[_transitionIndex].PrecursorMz
                    : _scanProvider.Transitions[_transitionIndex].ProductMz;
                xScale.Min = mz - 1.5;
                xScale.Max = mz + 3.5;
            }
            if (filterBtn.Checked && !spectrumBtn.Checked)
            {
                yScale.MinAuto = yScale.MaxAuto = false;
                double minDriftTime, maxDriftTime;
                GetDriftRange(out minDriftTime, out maxDriftTime);
                double range = maxDriftTime - minDriftTime;
                yScale.Min = minDriftTime - range/2;
                yScale.Max = maxDriftTime + range/2;
            }
            graphControl.GraphPane.AxisChange();
        }

        public void UpdateUI(bool selectionChanged = true)
        {
            // Only worry about updates, if the graph is visible
            // And make sure it is not disposed, since rendering happens on a timer
            if (!Visible || IsDisposed)
                return;

            if (_fullScans != null)
            {
                leftButton.Enabled = (_scanIndex > 0);
                rightButton.Enabled = (_scanIndex < _scanProvider.Times.Length-1);
                lblScanId.Text = GetScanId().ToString("D"); // Not L10N
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

        private void ChangeScan(int delta)
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

            LoadScan();
            UpdateUI(false);
        }

        private void rightButton_Click(object sender, EventArgs e)
        {
            ChangeScan(1);
        }

        private void leftButton_Click(object sender, EventArgs e)
        {
            ChangeScan(-1);
        }

        private void comboBoxScanType_SelectedIndexChanged(object sender, EventArgs e)
        {
            _source = SourceFromName(comboBoxScanType.Text);
            Zoom();
            LoadScan();
            UpdateUI(false);
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

        private void graphControl_ContextMenuBuilder(ZedGraphControl sender, ContextMenuStrip menuStrip, Point mousePt, ZedGraphControl.ContextMenuObjectState objState)
        {
            ZedGraphHelper.BuildContextMenu(graphControl, menuStrip, true);
        }

        private void magnifyBtn_CheckedChanged(object sender, EventArgs e)
        {
            ZoomToSelection(magnifyBtn.Checked);
        }

        public void ZoomToSelection(bool zoom)
        {
            Settings.Default.AutoZoomFullScanGraph = magnifyBtn.Checked = zoom;
            Zoom();
            CreateGraph();
        }

        private void spectrumBtn_CheckedChanged(object sender, EventArgs e)
        {
            SumScans(spectrumBtn.Checked);
        }

        public void SumScans(bool sum)
        {
            Settings.Default.SumScansFullScan = spectrumBtn.Checked = sum;
            Zoom();
            CreateGraph();
        }

        private void filterBtn_CheckedChanged(object sender, EventArgs e)
        {
            FilterDriftTimes(filterBtn.Checked);
        }

        public void FilterDriftTimes(bool filter)
        {
            Settings.Default.FilterDriftTimesFullScan = filterBtn.Checked = filter;
            Zoom();
            CreateGraph();            
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
                MessageDlg.Show(this, "Isolation window: {0}, {1}, {2}", low, target, high); // Not L10N
            }
        }

        /// <summary>
        /// Provides a constant background thread with responsibility for all interactions
        /// with <see cref="IScanProvider"/>, necessary because <see cref="MsDataFileImpl"/> objects
        /// must be accessed on the same thread.
        /// </summary>
        private class BackgroundScanProvider : IDisposable
        {
            private bool _disposing;
            private int _scanIdNext;
            private IScanProvider _scanProvider;
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
            /// Always run on a specific bakground thread to avoid changing threads when dealing
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

            public void SetScanProvider(IScanProvider scanProvider)
            {
                lock (this)
                {
                    if (_scanProvider != null)
                    {
                        if (scanProvider != null)
                            scanProvider.Adopt(_scanProvider);

                        // Queue for disposal
                        Assume.IsFalse(ReferenceEquals(scanProvider, _scanProvider));
                        _oldScanProviders.Add(_scanProvider);
                    }
                    _scanProvider = scanProvider;
                    Monitor.PulseAll(this);
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
                lock (this)
                {
                    foreach (var provider in _oldScanProviders)
                        provider.Dispose();
                    _oldScanProviders.Clear();
                }
            }

            public void Dispose()
            {
                // Wait for dispose to happen on the background thread
                lock (this)
                {
                    _disposing = true;
                    SetScanProvider(null);

                    while (_oldScanProviders.Any())
                        Monitor.Wait(this);
                }
            }
        }

        private static readonly int[] _heatMapColors =
        {
            0, 0, 255,
            0, 1, 255,
            0, 2, 255,
            0, 4, 255,
            0, 5, 255,
            0, 7, 255,
            0, 9, 255,
            0, 11, 255,
            0, 13, 255,
            0, 15, 255,
            0, 18, 253,
            0, 21, 251,
            0, 24, 250,
            0, 27, 248,
            0, 30, 245,
            0, 34, 243,
            0, 37, 240,
            0, 41, 237,
            0, 45, 234,
            0, 49, 230,
            0, 53, 226,
            0, 57, 222,
            0, 62, 218,
            0, 67, 214,
            0, 71, 209,
            0, 76, 204,
            0, 82, 199,
            0, 87, 193,
            0, 93, 188,
            0, 98, 182,
            0, 104, 175,
            0, 110, 169,
            0, 116, 162,
            7, 123, 155,
            21, 129, 148,
            34, 136, 141,
            47, 142, 133,
            60, 149, 125,
            71, 157, 117,
            83, 164, 109,
            93, 171, 100,
            104, 179, 91,
            113, 187, 92,
            123, 195, 73,
            132, 203, 63,
            140, 211, 53,
            148, 220, 43,
            156, 228, 33,
            163, 237, 22,
            170, 246, 11,
            176, 255, 0,
            183, 248, 0,
            188, 241, 0,
            194, 234, 0,
            199, 227, 0,
            204, 220, 0,
            209, 214, 0,
            213, 207, 0,
            217, 200, 0,
            221, 194, 0,
            224, 188, 0,
            227, 181, 0,
            230, 175, 0,
            233, 169, 0,
            236, 163, 0,
            238, 157, 0,
            240, 151, 0,
            243, 145, 0,
            244, 140, 0,
            246, 134, 0,
            248, 129, 0,
            249, 123, 0,
            250, 118, 0,
            251, 112, 0,
            252, 107, 0,
            253, 102, 0,
            254, 97, 0,
            255, 92, 0,
            255, 87, 0,
            255, 82, 0,
            255, 78, 0,
            255, 73, 0,
            255, 68, 0,
            255, 64, 0,
            255, 59, 0,
            255, 55, 0,
            255, 51, 0,
            255, 47, 0,
            255, 43, 0,
            255, 39, 0,
            255, 35, 0,
            255, 31, 0,
            255, 27, 0,
            255, 23, 0,
            255, 20, 0,
            255, 16, 0,
            255, 13, 0,
            255, 10, 0,
            255, 8, 0,
            255, 3, 0
        };
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

    public sealed class SelectedScanEventArgs : EventArgs
    {
        public SelectedScanEventArgs(MsDataFileUri dataFile, double retentionTime)
        {
            DataFile = dataFile;
            RetentionTime = retentionTime;
        }

        public MsDataFileUri DataFile { get; private set; }
        public double RetentionTime { get; private set; }
    }
}