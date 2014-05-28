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
        private readonly IDocumentUIContainer _documentContainer;
        private readonly GraphHelper _graphHelper;
        private readonly BackgroundScanProvider _scanProvider;
        private FullScan[] _fullScans;
        private string _fileName;
        private int _transitionIndex;
        private int _scanIndex;
        private readonly string[] _sourceNames;
        private ChromSource _source;
        private MsDataSpectrum _spectrum;

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

            magnifyBtn.Checked = Settings.Default.AutoZoomFullScanGraph;
        }

        private void SetScans(FullScan[] scans)
        {
            _fullScans = scans;

            GraphScan();
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

        private void GraphScan()
        {
            GraphPane.CurveList.Clear();
            GraphPane.GraphObjList.Clear();

            // Create a point list for each transition, and a default point list for points not 
            // associated with a transition.
            var pointLists = new PointPairList[_scanProvider.Transitions.Length];
            for (int i = 0; i < pointLists.Length; i++)
                pointLists[i] = new PointPairList();
            var defaultPointList = new PointPairList();

            // Assign each point to a transition point list, or else the default point list.
            _spectrum = _fullScans[0].Spectrum;
            for (int i = 0; i < _spectrum.Mzs.Length; i++)
            {
                double mz = _spectrum.Mzs[i];
                double intensity = _spectrum.Intensities[i];
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

            // Add extraction boxes.
            for (int i = 0; i < pointLists.Length; i++)
            {
                var transition = _scanProvider.Transitions[i];
                if (transition.Source != _source)
                    continue;
                var color1 = Blend(transition.Color, Color.White, 0.60);
                var color2 = Blend(transition.Color, Color.White, 0.95);
                var extractionBox = new BoxObj(
                    transition.ProductMz - transition.ExtractionWidth.Value/2,
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
            for (int i = 0; i < pointLists.Length; i++)
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
                label.FontSpec.Fill = new Fill(Color.FromArgb(100, Color.White));
                GraphPane.GraphObjList.Add(label);
            }

            double retentionTime = _spectrum.RetentionTime ?? _scanProvider.Times[_scanIndex];
            GraphPane.Title.Text = string.Format("{0} ({1:F2})", _fileName, retentionTime); // Not L10N
            UpdateUI();

            FireSelectedScanChanged(retentionTime);
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
            var axis = GraphPane.XAxis;
            if (magnifyBtn.Checked)
            {
                axis.Scale.MinAuto = false;
                axis.Scale.MaxAuto = false;
                double mz = _scanProvider.Source == ChromSource.ms1
                    ? _scanProvider.Transitions[_transitionIndex].PrecursorMz
                    : _scanProvider.Transitions[_transitionIndex].ProductMz;
                axis.Scale.Min = mz - 1.5;
                axis.Scale.Max = mz + 3.5;
            }
            else
            {
                axis.Scale.MinAuto = true;
                axis.Scale.MaxAuto = true;
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
            ZedGraphHelper.BuildContextMenu(graphControl, menuStrip);
        }

        private void magnify_Click(object sender, EventArgs e)
        {
            magnifyBtn.Checked = !magnifyBtn.Checked;
            Settings.Default.AutoZoomFullScanGraph = magnifyBtn.Checked;
            Zoom();
            GraphScan();
        }

        private void btnIsolationWindow_Click(object sender, EventArgs e)
        {
            var target = _spectrum.Precursors[0].IsolationWindowTargetMz;
            if (!target.HasValue)
                MessageDlg.Show(this, "No isolation target"); // Not L10N
            else
            {
                double low = target.Value - _spectrum.Precursors[0].IsolationWindowLower.Value;
                double high = target.Value + _spectrum.Precursors[0].IsolationWindowUpper.Value;
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
            private readonly Action<FullScan[]> _successAction;
            private readonly Action<Exception> _failureAction;

            public BackgroundScanProvider(Form form, Action<FullScan[]> successAction, Action<Exception> failureAction)
            {
                _scanIdNext = -1;

                _oldScanProviders = new List<IScanProvider>();
                _backgroundThread = new Thread(Work) { Name = GetType().Name, Priority = ThreadPriority.BelowNormal };
                _backgroundThread.Start();

                _form = form;
                _successAction = successAction;
                _failureAction = failureAction;
            }

            public MsDataFileUri DataFilePath { get { return _scanProvider.DataFilePath; } }
            public ChromSource Source { get { return _scanProvider.Source; } }
            public TransitionFullScanInfo[] Transitions { get { return _scanProvider.Transitions; } }
            public float[] Times { get { return _scanProvider.Times; } }

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