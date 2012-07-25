/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using DigitalRune.Windows.Docking;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Controls;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using ZedGraph;

namespace pwiz.Skyline
{
    public partial class SkylineWindow :
        GraphSpectrum.IStateProvider,
        GraphChromatogram.IStateProvider,
        GraphSummary.IStateProvider,
        ResultsGrid.IStateProvider
    {
        private GraphSpectrum _graphSpectrum;
        private readonly GraphSpectrumSettings _graphSpectrumSettings;
        private GraphSummary _graphRetentionTime;
        private GraphSummary _graphPeakArea;
        private ResultsGridForm _resultsGridForm;
        private readonly List<GraphChromatogram> _listGraphChrom = new List<GraphChromatogram>();
        private bool _inGraphUpdate;
        private int _alignToReplicate = -1;

        public RTGraphController RTGraphController
        {
            get
            {
                return (_graphRetentionTime != null ? (RTGraphController) _graphRetentionTime.Controller : null);
            }
        }

        private void dockPanel_ActiveDocumentChanged(object sender, EventArgs e)
        {
            ActiveDocumentChanged();
        }

        private void ActiveDocumentChanged()
        {
            if (DocumentUI == null)
                return;
            var settings = DocumentUI.Settings;
            if (_closing || ComboResults == null || ComboResults.IsDisposed || _inGraphUpdate || !settings.HasResults ||
                settings.MeasuredResults.Chromatograms.Count < 2)
                return;

            var activeForm = dockPanel.ActiveDocument;

            bool activeLibrary = ReferenceEquals(_graphSpectrum, activeForm);
            if (_graphPeakArea != null)
                _graphPeakArea.ActiveLibrary = activeLibrary;

            if (_graphRetentionTime != null)
                _graphRetentionTime.ActiveLibrary = activeLibrary;

            foreach (var graphChrom in _listGraphChrom)
            {
                if (ReferenceEquals(graphChrom, activeForm))
                    ComboResults.SelectedItem = graphChrom.TabText;
            }
        }

        private void graphsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowGraphSpectrum(Settings.Default.ShowSpectra = true);
        }

        private class DockPanelLayoutLock : IDisposable
        {
            private DockPanel _dockPanel;
            private Control _coverControl;
            private Cursor _cursorBegin;
            private bool _locked;

            public DockPanelLayoutLock(DockPanel dockPanel, bool startLocked = false)
            {
                _dockPanel = dockPanel;
                if (startLocked)
                    EnsureLocked();
            }

            /// <summary>
            /// Called to lock layout of the <see cref="DockPanel"/>.  Locking
            /// is defered until it is determined to be necessary to avoid the
            /// relayout calculation when locking is unnecessary.
            /// </summary>
            public void EnsureLocked()
            {
                if (!_locked)
                {
                    _locked = true;
                    _dockPanel.SuspendLayout(true);
                    _coverControl = new CoverControl(_dockPanel);
                    var cursorControl = _dockPanel.TopLevelControl ?? _dockPanel;
                    _cursorBegin = cursorControl.Cursor;
                    cursorControl.Cursor = Cursors.WaitCursor;
                }
            }

            public void Dispose()
            {
                if (_locked && _dockPanel != null)
                {
                    _dockPanel.ResumeLayout(true, true);
                    _coverControl.Dispose();
                    var cursorControl = _dockPanel.TopLevelControl ?? _dockPanel;
                    cursorControl.Cursor = _cursorBegin;
                }
                _dockPanel = null;  // Only once
            }
        }

        private bool HasPersistableLayout()
        {
            var settings = DocumentUI.Settings;
            return settings.HasResults || settings.PeptideSettings.Libraries.HasLibraries;
        }

        private void UpdateGraphUI(SrmSettings settingsOld, bool docIdChanged)
        {
            SrmSettings settingsNew = DocumentUI.Settings;
            if (ReferenceEquals(settingsNew, settingsOld))
            {
                // Just about any change could potentially change the list
                // or retention times or peak areas.
                if (settingsNew.HasResults)
                {
                    // UpdateGraphPanes can handle null values in the list, but
                    // only call it when at least one of the graphs is present.
                    if (_graphRetentionTime != null || _graphPeakArea != null)
                        UpdateGraphPanes(new List<IUpdatable> {_graphRetentionTime, _graphPeakArea});
                }
                return;                
            }
            var listUpdateGraphs = new List<IUpdatable>();
            var filterNew = settingsNew.TransitionSettings.Filter;
            var filterOld = settingsOld.TransitionSettings.Filter;
            if (!ReferenceEquals(filterNew, filterOld))
            {
                // If ion types or charges changed, make sure the new
                // ones are on and the old ones are off by default.
                bool refresh = false;
                if (!ArrayUtil.EqualsDeep(filterNew.IonTypes, filterOld.IonTypes))
                {
                    // Only turn off old ion types, if new settings are not MS1-only full-scan
                    var fullScan = settingsNew.TransitionSettings.FullScan;
                    if (!fullScan.IsEnabled || fullScan.IsEnabledMsMs)
                        CheckIonTypes(filterOld.IonTypes, false);

                    CheckIonTypes(filterNew.IonTypes, true);
                    refresh = true;
                }
                if (!ArrayUtil.EqualsDeep(filterNew.ProductCharges, filterOld.ProductCharges))
                {
                    CheckIonCharges(filterOld.ProductCharges, false);
                    CheckIonCharges(filterNew.ProductCharges, true);
                    refresh = true;
                }
                if (refresh && _graphSpectrum != null)
                    listUpdateGraphs.Add(_graphSpectrum);
            }

            using (var layoutLock = new DockPanelLayoutLock(dockPanel))
            {
                bool deserialized = false;
                string layoutFile = GetViewFile(DocumentFilePath);
                if (docIdChanged && File.Exists(layoutFile))
                {
                    layoutLock.EnsureLocked();
                    try
                    {
                        using (var layoutReader = new StreamReader(layoutFile))
                        {
                            LoadLayout(layoutReader.BaseStream);
                        }
                        deserialized = true;
                    }
                    catch (Exception x)
                    {
                        throw new IOException(
                            string.Format(
                                "Failure attempting to load the window layout file {0}.\nRename or delete this file to restore the default layout.\nSkyline may also need to be restarted.",
                                layoutFile), x);
                    }
                }

                bool enable = DocumentUI.Settings.PeptideSettings.Libraries.HasLibraries;
                if (graphsToolStripMenuItem.Enabled != enable)
                {
                    graphsToolStripMenuItem.Enabled = enable;
                    ionTypesMenuItem.Enabled = enable;
                    chargesMenuItem.Enabled = enable;
                    ranksMenuItem.Enabled = enable;

                    if (!deserialized)
                    {
                        layoutLock.EnsureLocked();
                        ShowGraphSpectrum(enable && Settings.Default.ShowSpectra);
                    }
                }
                enable = DocumentUI.Settings.HasResults;
                bool enableSchedule = enable || DocumentUI.Settings.PeptideSettings.Prediction.RetentionTime != null;
                if (retentionTimesMenuItem.Enabled != enableSchedule || replicateComparisonMenuItem.Enabled != enable)
                {
                    retentionTimesMenuItem.Enabled = enableSchedule;
                    replicateComparisonMenuItem.Enabled = enable;
                    timePeptideComparisonMenuItem.Enabled = enable;
                    linearRegressionMenuItem.Enabled = enable;
                    schedulingMenuItem.Enabled = enableSchedule;

                    if (!deserialized)
                    {
                        layoutLock.EnsureLocked();
                        ShowGraphRetentionTime(enable && Settings.Default.ShowRetentionTimeGraph);
                    }
                }
                if (resultsGridMenuItem.Enabled != enable)
                {
                    resultsGridMenuItem.Enabled = enable;
                    if (!deserialized)
                    {
                        layoutLock.EnsureLocked();
                        ShowResultsGrid(enable && Settings.Default.ShowResultsGrid);
                    }
                }
                if (peakAreasMenuItem.Enabled != enable)
                {
                    peakAreasMenuItem.Enabled = enable;
                    areaReplicateComparisonMenuItem.Enabled = enable;
                    areaPeptideComparisonMenuItem.Enabled = enable;

                    if (!deserialized)
                    {
                        layoutLock.EnsureLocked();
                        ShowGraphPeakArea(enable && Settings.Default.ShowPeakAreaGraph);
                    }
                }

                if (!ReferenceEquals(settingsNew.MeasuredResults, settingsOld.MeasuredResults))
                {
                    // First hide all graph windows for results that no longer exist in the document
                    foreach (var graphChromatogram in _listGraphChrom.ToArray())
                    {
                        string name = graphChromatogram.NameSet;
                        // Look for mathcing chromatogram sets across the documents
                        ChromatogramSet chromSetOld;
                        ChromatogramSet chromSetNew;
                        int index;
                        if (settingsOld.HasResults &&
                            settingsOld.MeasuredResults.TryGetChromatogramSet(name, out chromSetOld, out index) &&
                            settingsNew.HasResults &&
                            settingsNew.MeasuredResults.TryGetChromatogramSet(chromSetOld.Id.GlobalIndex, out chromSetNew, out index))
                        {
                            // If matching chromatogram found, but name has changed, then
                            // update the graph pane
                            if (!Equals(chromSetNew.Name, chromSetOld.Name))
                                name = graphChromatogram.NameSet = chromSetNew.Name;
                        }
                        var results = settingsNew.MeasuredResults;
                        if (results == null || !results.Chromatograms.Contains(chrom => Equals(chrom.Name, name)))
                        {
                            layoutLock.EnsureLocked();
                            ShowGraphChrom(graphChromatogram.NameSet, false);
                            // If changed to a new document, destroy unused panes
                            if (docIdChanged)
                            {
                                var graphChrom = GetGraphChrom(name);
                                _listGraphChrom.Remove(graphChrom);
                                if (graphChrom != null)
                                    DestroyGraphChrom(graphChrom);
                            }
                        }
                    }
                    // Next show any graph windows for results that were not previously part of
                    // the document.
                    if (settingsNew.MeasuredResults != null && !deserialized)
                    {
                        // Keep changes in graph panes from stealing the focus
                        var focusStart = User32.GetFocusedControl();

                        _inGraphUpdate = true;
                        try
                        {
                            string nameFirst = null;
                            string nameLast = SelectedGraphChromName;

                            foreach (var chromatogram in settingsNew.MeasuredResults.Chromatograms)
                            {
                                string name = chromatogram.Name;
                                var graphChrom = GetGraphChrom(name);
                                if (graphChrom == null)
                                {
                                    layoutLock.EnsureLocked();
                                    CreateGraphChrom(name, nameLast, false);

                                    nameFirst = nameFirst ?? name;
                                    nameLast = name;
                                }
                                    // If the pane is not showing a tab for this graph, than add one.
                                else if (graphChrom.Pane == null ||
                                         !graphChrom.Pane.DisplayingContents.Contains(graphChrom))
                                {
                                    layoutLock.EnsureLocked();
                                    ShowGraphChrom(name, true);

                                    nameFirst = nameFirst ?? name;
                                    nameLast = name;
                                }
                            }

                            // Put the first set on top, since it will get populated with data first
                            if (nameFirst != null)
                            {
                                layoutLock.EnsureLocked();
                                ShowGraphChrom(nameFirst, true);                                
                            }
                        }
                        finally
                        {
                            _inGraphUpdate = false;
                        }

                        if (focusStart != null)
                            focusStart.Focus();
                    }

                    // Update displayed graphs, which are no longer valid
                    var listGraphUpdate = from graph in _listGraphChrom
                                          where graph.Visible
                                          where !graph.IsCurrent
                                          select graph;
                    listUpdateGraphs.AddRange(listGraphUpdate.ToArray());

                    // Make sure view menu is correctly enabled
                    bool enabled = settingsNew.HasResults;
                    chromatogramsMenuItem.Enabled = enabled;
                    transitionsMenuItem.Enabled = enabled;
                    transformChromMenuItem.Enabled = enabled;
                    autoZoomMenuItem.Enabled = enabled;
                    arrangeGraphsToolStripMenuItem.Enabled = enabled;

//                    UpdateReplicateMenuItems(enabled);

                    // CONSIDER: Enable/disable submenus too?
                }
                else if (!ReferenceEquals(settingsNew.PeptideSettings.Prediction.RetentionTime,
                                          settingsOld.PeptideSettings.Prediction.RetentionTime))
                {
                    // If retention time regression changed, and retention prediction is showing
                    // update all displayed graphs
                    if (Settings.Default.ShowRetentionTimePred)
                        listUpdateGraphs.AddRange(_listGraphChrom.ToArray());
                }
            } // layoutLock.Dispose()

            // Do this after layout is unlocked, because it messes up the selected graph otherwise
            if (_sequenceTreeForm == null)
            {
                ShowSequenceTreeForm(true);
            }

            // Just about any change could potentially change these panes.
            if (settingsNew.HasResults)
            {
                if (_graphRetentionTime != null)
                    listUpdateGraphs.Add(_graphRetentionTime);
                if (_graphPeakArea != null)
                    listUpdateGraphs.Add(_graphPeakArea);                
                if (_resultsGridForm != null)
                    listUpdateGraphs.Add(_resultsGridForm);
            }

            UpdateGraphPanes(listUpdateGraphs);
        }

        // Load view layout from the given stream.
        public void LoadLayout(Stream layoutStream)
        {
            using (new DockPanelLayoutLock(dockPanel, true))
            {
                LoadLayoutLocked(layoutStream);
            }
        }

        // Load view layout from the given stream.
        private void LoadLayoutLocked(Stream layoutStream)
        {
            // Get rid of any existing graph windows, since the layout
            // deserialization has problems using existing windows.
            DestroySequenceTreeForm();
            DestroyGraphSpectrum();
            DestroyGraphRetentionTime();
            DestroyGraphPeakArea();
            DestroyResultsGrid();
            foreach (GraphChromatogram graphChrom in _listGraphChrom)
                DestroyGraphChrom(graphChrom);
            _listGraphChrom.Clear();
            dockPanel.LoadFromXml(layoutStream, DeserializeForm);
            EnsureFloatingWindowsVisible();
        }

        public void InvalidateChromatogramGraphs()
        {
            _listGraphChrom.ForEach(graph => graph.IsCacheInvalidated = true);
        }

        private void EnsureFloatingWindowsVisible()
        {
            foreach (var floatingWindow in dockPanel.FloatingWindows)
            {
                var screen = Screen.FromControl(floatingWindow);
                var rectScreen = screen.WorkingArea;
                var rectWindow = floatingWindow.Bounds;
                rectWindow.Width = Math.Min(rectWindow.Width, rectScreen.Width);
                rectWindow.Height = Math.Min(rectWindow.Height, rectScreen.Height);
                rectWindow.X = Math.Max(rectScreen.X,
                    Math.Min(rectWindow.X, rectScreen.X + rectScreen.Width - rectWindow.Width));
                rectWindow.Y = Math.Max(rectScreen.Y,
                    Math.Min(rectWindow.Y, rectScreen.Y + rectScreen.Height - rectWindow.Height));
                if (!Equals(rectWindow, floatingWindow.Bounds))
                    floatingWindow.Bounds = rectWindow;
            }
        }

        private IDockableForm DeserializeForm(string persistentString)
        {
            if (Equals(persistentString, typeof(SequenceTreeForm).ToString()))
            {
                return _sequenceTreeForm ?? CreateSequenceTreeForm();
            }
            else if (Equals(persistentString, typeof(GraphSpectrum).ToString()))
            {
                return _graphSpectrum ?? CreateGraphSpectrum();                
            }
            if (persistentString.EndsWith("Skyline.Controls.GraphRetentionTime") ||  // Backward compatibility
                (persistentString.StartsWith(typeof(GraphSummary).ToString()) &&
                 persistentString.EndsWith(typeof(RTGraphController).Name)))
            {
                return _graphRetentionTime ?? CreateGraphRetentionTime();                
            }
            if (persistentString.EndsWith("Skyline.Controls.GraphPeakArea") ||  // Backward compatibility
                (persistentString.StartsWith(typeof(GraphSummary).ToString()) &&
                 persistentString.EndsWith(typeof(AreaGraphController).Name)))
            {
                return _graphPeakArea ?? CreateGraphPeakArea();                
            }
            if (Equals(persistentString, typeof(ResultsGridForm).ToString()))
            {
                return _resultsGridForm ?? CreateResultsGrid();
            }
            if (persistentString.StartsWith(typeof(GraphChromatogram).ToString()))
            {
                string name = GraphChromatogram.GetTabText(persistentString);
                var settings = DocumentUI.Settings;
                if (settings.HasResults && settings.MeasuredResults.ContainsChromatogram(name))
                    return GetGraphChrom(name) ?? CreateGraphChrom(name);
            }
            return null;
        }

        // Disabling these menuitems allows the normal meaning of Ctrl-Up/Down
        // to cause scrolling in the tree view.
//        private void UpdateReplicateMenuItems(bool hasResults)
//        {
//            nextReplicateMenuItem.Enabled = hasResults && comboResults.SelectedIndex < comboResults.Items.Count - 1;
//            previousReplicateMenuItem.Enabled = hasResults && comboResults.SelectedIndex > 0;
//        }

        private void UpdateGraphPanes()
        {
            // Add only visible graphs to the update list, since each update
            // must pass through the Windows message queue on a WM_TIMER.
            var listVisibleChrom = from graphChrom in _listGraphChrom
                                   where graphChrom.Visible
                                   select graphChrom;

            var listUpdateGraphs = new List<IUpdatable>(listVisibleChrom.ToArray());
            
            if (_graphSpectrum != null && _graphSpectrum.Visible)
                listUpdateGraphs.Add(_graphSpectrum);
            if (_graphRetentionTime != null && _graphRetentionTime.Visible)
                listUpdateGraphs.Add(_graphRetentionTime);
            if (_graphPeakArea != null && _graphPeakArea.Visible)
                listUpdateGraphs.Add(_graphPeakArea);
            if (_resultsGridForm != null && _resultsGridForm.Visible)
                listUpdateGraphs.Add(_resultsGridForm);

            UpdateGraphPanes(listUpdateGraphs);
        }

        private void UpdateGraphPanes(ICollection<IUpdatable> graphPanes)
        {
            if (graphPanes.Count == 0)
                return;
            // Restart the timer at 100ms, giving the UI time to interrupt.
            _timerGraphs.Stop();
            _timerGraphs.Interval = 100;
            _timerGraphs.Tag = graphPanes;
            _timerGraphs.Start();
        }

        private void UpdateGraphPanes(object sender, EventArgs e)
        {
            // Stop the timer immediately, to keep from getting called again
            // for the same triggering event.
            _timerGraphs.Stop();

            IList<IUpdatable> listGraphPanes = (IList<IUpdatable>)_timerGraphs.Tag;
            int count = 0;
            if (listGraphPanes != null && listGraphPanes.Count > 0)
            {
                // Allow nulls in the list
                while (listGraphPanes.Count > 0 && listGraphPanes[0] == null)
                    listGraphPanes.RemoveAt(0);

                if (listGraphPanes.Count > 0)
                {
                    listGraphPanes[0].UpdateUI();
                    listGraphPanes.RemoveAt(0);                    
                }
                count = listGraphPanes.Count;
            }
            if (count != 0)
            {
                // If more graphs to update, update them as quickly as possible.
                _timerGraphs.Interval = 1;
                _timerGraphs.Start();
            }
        }

        /// <summary>
        /// Returns true if the graph panels still need to be updated to show the current selection.
        /// Used for testing. 
        /// </summary>
        public bool IsGraphUpdatePending
        {
            get
            {
                // The implementation of "UpdateGraphs" is such that this question should only be
                // asked on the event thread
                if (InvokeRequired)
                {
                    throw new InvalidOperationException("Must be called from event thread");
                }
                if (_timerGraphs.Enabled)
                {
                    return true;
                }
                return false;
            }
        }

        public int AlignToReplicate
        {
            get { return _alignToReplicate; }
            set 
            { 
                if (value == AlignToReplicate)
                {
                    return;
                }
                _alignToReplicate = value;
                UpdateGraphPanes();
            }
        }

        #region Spectrum graph

        public GraphSpectrum GraphSpectrum { get { return _graphSpectrum; } }
        public GraphSpectrumSettings GraphSpectrumSettings { get { return _graphSpectrumSettings; } }

        private void aMenuItem_Click(object sender, EventArgs e)
        {
            _graphSpectrumSettings.ShowAIons = !_graphSpectrumSettings.ShowAIons;
        }

        private void bMenuItem_Click(object sender, EventArgs e)
        {
            _graphSpectrumSettings.ShowBIons = !_graphSpectrumSettings.ShowBIons;
        }

        private void cMenuItem_Click(object sender, EventArgs e)
        {
            _graphSpectrumSettings.ShowCIons = !_graphSpectrumSettings.ShowCIons;
        }

        private void xMenuItem_Click(object sender, EventArgs e)
        {
            _graphSpectrumSettings.ShowXIons = !_graphSpectrumSettings.ShowXIons;
        }

        private void yMenuItem_Click(object sender, EventArgs e)
        {
            _graphSpectrumSettings.ShowYIons = !_graphSpectrumSettings.ShowYIons;
        }

        private void zMenuItem_Click(object sender, EventArgs e)
        {
            _graphSpectrumSettings.ShowZIons = !_graphSpectrumSettings.ShowZIons;
        }

        private void precursorIonMenuItem_Click(object sender, EventArgs e)
        {
            _graphSpectrumSettings.ShowPrecursorIon = !_graphSpectrumSettings.ShowPrecursorIon;
        }

        private void ionTypesMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            var set = Settings.Default;
            aMenuItem.Checked = aionsContextMenuItem.Checked = set.ShowAIons;
            bMenuItem.Checked = bionsContextMenuItem.Checked = set.ShowBIons;
            cMenuItem.Checked = cionsContextMenuItem.Checked = set.ShowCIons;
            xMenuItem.Checked = xionsContextMenuItem.Checked = set.ShowXIons;
            yMenuItem.Checked = yionsContextMenuItem.Checked = set.ShowYIons;
            zMenuItem.Checked = zionsContextMenuItem.Checked = set.ShowZIons;
            precursorIonMenuItem.Checked = precursorIonContextMenuItem.Checked = set.ShowPrecursorIon;
        }

        private void charge1MenuItem_Click(object sender, EventArgs e)
        {
            _graphSpectrumSettings.ShowCharge1 = !_graphSpectrumSettings.ShowCharge1;
        }

        private void charge2MenuItem_Click(object sender, EventArgs e)
        {
            _graphSpectrumSettings.ShowCharge2 = !_graphSpectrumSettings.ShowCharge2;
        }

        private void charge3MenuItem_Click(object sender, EventArgs e)
        {
            _graphSpectrumSettings.ShowCharge3 = !_graphSpectrumSettings.ShowCharge3;
        }

        private void charge4MenuItem_Click(object sender, EventArgs e)
        {
            _graphSpectrumSettings.ShowCharge4 = !_graphSpectrumSettings.ShowCharge4;
        }

        private void chargesMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            var set = _graphSpectrumSettings;
            charge1MenuItem.Checked = charge1ContextMenuItem.Checked = set.ShowCharge1;
            charge2MenuItem.Checked = charge2ContextMenuItem.Checked = set.ShowCharge2;
            charge3MenuItem.Checked = charge3ContextMenuItem.Checked = set.ShowCharge3;
            charge4MenuItem.Checked = charge4ContextMenuItem.Checked = set.ShowCharge4;
        }

        private void viewToolStripMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            ranksMenuItem.Checked = ranksContextMenuItem.Checked = Settings.Default.ShowRanks;
        }

        private void ranksMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.ShowRanks = !Settings.Default.ShowRanks;
            UpdateSpectrumGraph();
        }

        private void ionMzValuesContextMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.ShowIonMz = !Settings.Default.ShowIonMz;
            UpdateSpectrumGraph();
        }

        private void observedMzValuesContextMenuItem_Click(object sender, EventArgs e)
        {
            ToggleObservedMzValues();
        }

        public void ToggleObservedMzValues()
        {
            Settings.Default.ShowObservedMz = !Settings.Default.ShowObservedMz;
            UpdateSpectrumGraph();
        }

        void GraphSpectrum.IStateProvider.BuildSpectrumMenu(ZedGraphControl zedGraphControl, ContextMenuStrip menuStrip)
        {
            // Store original menuitems in an array, and insert a separator
            ToolStripItem[] items = new ToolStripItem[menuStrip.Items.Count];
            int iUnzoom = -1;
            for (int i = 0; i < items.Length; i++)
            {
                items[i] = menuStrip.Items[i];
                string tag = (string)items[i].Tag;
                if (tag == "unzoom")
                    iUnzoom = i;
            }

            if (iUnzoom != -1)
                menuStrip.Items.Insert(iUnzoom, toolStripSeparator27);

            // Insert skyline specific menus
            var set = Settings.Default;
            int iInsert = 0;
            aionsContextMenuItem.Checked = set.ShowAIons;
            menuStrip.Items.Insert(iInsert++, aionsContextMenuItem);
            bionsContextMenuItem.Checked = set.ShowBIons;
            menuStrip.Items.Insert(iInsert++, bionsContextMenuItem);
            cionsContextMenuItem.Checked = set.ShowCIons;
            menuStrip.Items.Insert(iInsert++, cionsContextMenuItem);
            xionsContextMenuItem.Checked = set.ShowXIons;
            menuStrip.Items.Insert(iInsert++, xionsContextMenuItem);
            yionsContextMenuItem.Checked = set.ShowYIons;
            menuStrip.Items.Insert(iInsert++, yionsContextMenuItem);
            zionsContextMenuItem.Checked = set.ShowZIons;
            menuStrip.Items.Insert(iInsert++, zionsContextMenuItem);
            precursorIonContextMenuItem.Checked = set.ShowPrecursorIon;
            menuStrip.Items.Insert(iInsert++, precursorIonContextMenuItem);
            menuStrip.Items.Insert(iInsert++, toolStripSeparator11);
            menuStrip.Items.Insert(iInsert++, chargesContextMenuItem);
            if (chargesContextMenuItem.DropDownItems.Count == 0)
            {
                chargesContextMenuItem.DropDownItems.AddRange(new ToolStripItem[]
                    {
                        charge1ContextMenuItem,
                        charge2ContextMenuItem,
                        charge3ContextMenuItem,
                        charge4ContextMenuItem,
                    });
            }
            menuStrip.Items.Insert(iInsert++, toolStripSeparator12);
            ranksContextMenuItem.Checked = set.ShowRanks;
            menuStrip.Items.Insert(iInsert++, ranksContextMenuItem);
            ionMzValuesContextMenuItem.Checked = set.ShowIonMz;
            menuStrip.Items.Insert(iInsert++, ionMzValuesContextMenuItem);
            observedMzValuesContextMenuItem.Checked = set.ShowObservedMz;
            menuStrip.Items.Insert(iInsert++, observedMzValuesContextMenuItem);
            duplicatesContextMenuItem.Checked = set.ShowDuplicateIons;
            menuStrip.Items.Insert(iInsert++, duplicatesContextMenuItem);
            menuStrip.Items.Insert(iInsert++, toolStripSeparator13);
            lockYaxisContextMenuItem.Checked = set.LockYAxis;
            menuStrip.Items.Insert(iInsert++, lockYaxisContextMenuItem);
            menuStrip.Items.Insert(iInsert++, toolStripSeparator14);
            menuStrip.Items.Insert(iInsert++, spectrumPropsContextMenuItem);
            menuStrip.Items.Insert(iInsert, toolStripSeparator15);

            // Remove some ZedGraph menu items not of interest
            foreach (var item in items)
            {
                string tag = (string)item.Tag;
                if (tag == "set_default" || tag == "show_val")
                    menuStrip.Items.Remove(item);
            }
            CopyEmfToolStripMenuItem.AddToContextMenu(zedGraphControl, menuStrip);
        }

        private void duplicatesContextMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.ShowDuplicateIons = duplicatesContextMenuItem.Checked;
            UpdateSpectrumGraph();
        }

        private void lockYaxisContextMenuItem_Click(object sender, EventArgs e)
        {
            // Avoid updating the rest of the graph just to change the y-axis lock state
            _graphSpectrum.LockYAxis(Settings.Default.LockYAxis = lockYaxisContextMenuItem.Checked);
        }

        private void spectrumPropsContextMenuItem_Click(object sender, EventArgs e)
        {
            using (var dlg = new SpectrumChartPropertyDlg())
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    UpdateSpectrumGraph();
            }
        }

        private void zoomSpectrumContextMenuItem_Click(object sender, EventArgs e)
        {
            if (_graphSpectrum != null)
                _graphSpectrum.ZoomSpectrumToSettings();
        }

        public void ShowGraphSpectrum(bool show)
        {
            if (show)
            {
                if (_graphSpectrum != null)
                {
                    _graphSpectrum.Activate();
                    _graphSpectrum.Focus();
                }
                else
                {
                    _graphSpectrum = CreateGraphSpectrum();
                    int firstDocumentPane = FirstDocumentPane;
                    if (firstDocumentPane == -1)
                        _graphSpectrum.Show(dockPanel, DockState.Document);
                    else
                        _graphSpectrum.Show(dockPanel.Panes[firstDocumentPane], DockPaneAlignment.Right, 0.5);
                }
            }
            else if (_graphSpectrum != null)
            {
                // Save current setting for showing spectra
                show = Settings.Default.ShowSpectra;
                // Close the spectrum graph window
                _graphSpectrum.Hide();
                // Restore setting and menuitem from saved value
                Settings.Default.ShowSpectra = show;
            }
        }

        // Testing
        public bool GraphSpectrumVisible()
        {
            return _graphSpectrum != null && _graphSpectrum.Visible;
        }

        private GraphSpectrum CreateGraphSpectrum()
        {
            // Create a new spectrum graph
            _graphSpectrum = new GraphSpectrum(this);
            _graphSpectrum.UpdateUI();
            _graphSpectrum.FormClosed += graphSpectrum_FormClosed;
            _graphSpectrum.VisibleChanged += graphSpectrum_VisibleChanged;
            _graphSpectrum.SelectedSpectrumChanged += graphSpectrum_SelectedSpectrumChanged;
            return _graphSpectrum;
        }

        private void DestroyGraphSpectrum()
        {
            if (_graphSpectrum != null)
            {
                _graphSpectrum.FormClosed -= graphSpectrum_FormClosed;
                _graphSpectrum.VisibleChanged -= graphSpectrum_VisibleChanged;
                _graphSpectrum.SelectedSpectrumChanged -= graphSpectrum_SelectedSpectrumChanged;
                _graphSpectrum.Close();
                _graphSpectrum = null;
            }
        }

        private void graphSpectrum_VisibleChanged(object sender, EventArgs e)
        {
            Settings.Default.ShowSpectra = _graphSpectrum.Visible;
        }

        private void graphSpectrum_FormClosed(object sender, FormClosedEventArgs e)
        {
            // Update settings and menu check
            Settings.Default.ShowSpectra = false;
            _graphSpectrum = null;
        }

        private void graphSpectrum_SelectedSpectrumChanged(object sender, SelectedSpectrumEventArgs e)
        {
            // Might need to update the selected MS/MS spectrum, if full-scan
            // filtering was used.
            if (DocumentUI.Settings.TransitionSettings.FullScan.IsEnabled &&
                    DocumentUI.Settings.HasResults)
            {
                if (e.Spectrum != null && e.IsUserAction)
                {
                    // Activate the selected replicate, if there is one associated with
                    // the selected spectrum.
                    string replicateName = e.Spectrum.ReplicateName;
                    if (!string.IsNullOrEmpty(replicateName))
                    {
                        int resultsIndex = DocumentUI.Settings.MeasuredResults.Chromatograms.IndexOf(chrom =>
                            Equals(replicateName, chrom.Name));
                        if (resultsIndex != -1)
                            SelectedResultsIndex = resultsIndex;
                    }
                }

                UpdateChromGraphs();
            }
        }

        private void UpdateSpectrumGraph()
        {
            if (_graphSpectrum != null)
                _graphSpectrum.UpdateUI();
        }

//        private static bool SameChargeGroups(PeptideTreeNode nodeTree)
//        {
//            // Check to see if all transition groups under a peptide tree node
//            // have the same precursor charge.
//            int charge = 0;
//            foreach (TransitionGroupDocNode nodeGroup in nodeTree.DocNode.Children)
//            {
//                if (charge == 0)
//                    charge = nodeGroup.TransitionGroup.PrecursorCharge;
//                else if (charge != nodeGroup.TransitionGroup.PrecursorCharge)
//                    return false;
//            }
//            // True only if there was at least one group
//            return (charge != 0);
//        }

        IList<IonType> GraphSpectrum.IStateProvider.ShowIonTypes
        {
            get { return _graphSpectrumSettings.ShowIonTypes; }
        }

        private void CheckIonTypes(IEnumerable<IonType> types, bool check)
        {
            foreach (var type in types)
                CheckIonType(type, check);
        }

        private void CheckIonType(IonType type, bool check)
        {
            var set = Settings.Default;
            switch (type)
            {
                case IonType.a: set.ShowAIons = aMenuItem.Checked = check; break;
                case IonType.b: set.ShowBIons = bMenuItem.Checked = check; break;
                case IonType.c: set.ShowCIons = cMenuItem.Checked = check; break;
                case IonType.x: set.ShowXIons = xMenuItem.Checked = check; break;
                case IonType.y: set.ShowYIons = yMenuItem.Checked = check; break;
                case IonType.z: set.ShowZIons = zMenuItem.Checked = check; break;
            }
        }

        IList<int> GraphSpectrum.IStateProvider.ShowIonCharges
        {
            get { return _graphSpectrumSettings.ShowIonCharges; }
        }

        private void CheckIonCharges(IEnumerable<int> charges, bool check)
        {
            foreach (int charge in charges)
                CheckIonCharge(charge, check);
        }

        private void CheckIonCharge(int charge, bool check)
        {
            // Set charge settings without causing UI to update
            var set = Settings.Default;
            switch (charge)
            {
                case 1: set.ShowCharge1 = charge1MenuItem.Checked = check; break;
                case 2: set.ShowCharge2 = charge2MenuItem.Checked = check; break;
                case 3: set.ShowCharge3 = charge3MenuItem.Checked = check; break;
                case 4: set.ShowCharge4 = charge4MenuItem.Checked = check; break;
            }
        }

        #endregion

        #region Chromatogram graphs

        private void chromatogramsMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            ToolStripMenuItem menu = chromatogramsMenuItem;
            if (!DocumentUI.Settings.HasResults)
            {
                // Strange problem in .NET where a dropdown will show when
                // its menuitem is disabled.
                chromatogramsMenuItem.HideDropDown();
                return;
            }

            // If MeasuredResults is null, then this menuitem is incorrectly enabled
            var chromatograms = DocumentUI.Settings.MeasuredResults.Chromatograms;

            int i = 0;
            foreach (var chrom in chromatograms)
            {
                string name = chrom.Name;
                ToolStripMenuItem item = null;
                if (i < menu.DropDownItems.Count)
                    item = menu.DropDownItems[i] as ToolStripMenuItem;
                if (item == null || name != item.Name)
                {
                    // Remove the rest of the existing items
                    while (!ReferenceEquals(menu.DropDownItems[i], toolStripSeparatorReplicates))
                        menu.DropDownItems.RemoveAt(i);

                    ShowChromHandler handler = new ShowChromHandler(this, chrom.Name);
                    item = new ToolStripMenuItem(chrom.Name, null,
                        handler.menuItem_Click);
                    menu.DropDownItems.Insert(i, item);
                }

                i++;
            }

            // Remove the rest of the existing items
            while (!ReferenceEquals(menu.DropDownItems[i], toolStripSeparatorReplicates))
                menu.DropDownItems.RemoveAt(i);
        }

        private class ShowChromHandler
        {
            private readonly SkylineWindow _skyline;
            private readonly string _nameChromatogram;

            public ShowChromHandler(SkylineWindow skyline, string nameChromatogram)
            {
                _skyline = skyline;
                _nameChromatogram = nameChromatogram;
            }

            public void menuItem_Click(object sender, EventArgs e)
            {
                _skyline.ShowGraphChrom(_nameChromatogram, true);
            }
        }

        void GraphChromatogram.IStateProvider.BuildChromatogramMenu(ZedGraphControl zedGraphControl, ContextMenuStrip menuStrip)
        {
            // Store original menuitems in an array, and insert a separator
            ToolStripItem[] items = new ToolStripItem[menuStrip.Items.Count];
            int iUnzoom = -1;
            for (int i = 0; i < items.Length; i++)
            {
                items[i] = menuStrip.Items[i];
                string tag = (string)items[i].Tag;
                if (tag == "unzoom")
                    iUnzoom = i;
            }

            if (iUnzoom != -1)
                menuStrip.Items.Insert(iUnzoom, toolStripSeparator26);

            // Insert skyline specific menus
            var settings = DocumentUI.Settings;
            bool retentionPredict = (settings.PeptideSettings.Prediction.RetentionTime != null);
            bool peptideIdTimes = (settings.PeptideSettings.Libraries.HasLibraries &&
                                   settings.TransitionSettings.FullScan.IsEnabled);
            var set = Settings.Default;
            int iInsert = 0;
            var nodeTree = SelectedNode;
            if (nodeTree is TransitionTreeNode && GraphChromatogram.IsSingleTransitionDisplay)
            {
                if (HasPeak(SelectedResultsIndex, ((TransitionTreeNode)nodeTree).DocNode))
                {
                    menuStrip.Items.Insert(iInsert++, removePeakGraphMenuItem);
                    menuStrip.Items.Insert(iInsert++, toolStripSeparator33);                    
                }
            }
            else if ((nodeTree is TransitionTreeNode && GraphChromatogram.GetDisplayType(DocumentUI) == DisplayTypeChrom.all) ||
                    (nodeTree is TransitionGroupTreeNode) ||
                    (nodeTree is PeptideTreeNode && ((PeptideTreeNode)nodeTree).DocNode.Children.Count == 1))
            {
                var nodeGroupTree = SequenceTree.GetNodeOfType<TransitionGroupTreeNode>();
                var nodeGroup = nodeGroupTree != null ? nodeGroupTree.DocNode :
                    (TransitionGroupDocNode)((PeptideTreeNode)nodeTree).ChildDocNodes[0];
                if (HasPeak(SelectedResultsIndex, nodeGroup))
                {
                    menuStrip.Items.Insert(iInsert++, removePeaksGraphMenuItem);
                    menuStrip.Items.Insert(iInsert++, toolStripSeparator33);                    
                }
            }
            legendChromContextMenuItem.Checked = set.ShowChromatogramLegend;
            menuStrip.Items.Insert(iInsert++, legendChromContextMenuItem);
            peakBoundariesContextMenuItem.Checked = set.ShowPeakBoundaries;
            menuStrip.Items.Insert(iInsert++, peakBoundariesContextMenuItem);
            menuStrip.Items.Insert(iInsert++, retentionTimesContextMenuItem);
            if (retentionTimesContextMenuItem.DropDownItems.Count == 0)
            {
                retentionTimesContextMenuItem.DropDownItems.AddRange(new ToolStripItem[]
                    {
                        allRTContextMenuItem,
                        bestRTContextMenuItem,
                        thresholdRTContextMenuItem,
                        noneRTContextMenuItem     
                    });
            }
            if (retentionPredict)
            {
                retentionTimePredContextMenuItem.Checked = set.ShowRetentionTimePred;
                menuStrip.Items.Insert(iInsert++, retentionTimePredContextMenuItem);
            }
            if (peptideIdTimes)
            {
                peptideIDTimesContextMenuItem.Checked = set.ShowPeptideIdTimes;
                menuStrip.Items.Insert(iInsert++, peptideIDTimesContextMenuItem);
                alignedPeptideIDTimesToolStripMenuItem.Checked = set.ShowAlignedPeptideIdTimes;
                menuStrip.Items.Insert(iInsert++, alignedPeptideIDTimesToolStripMenuItem);
            }
            menuStrip.Items.Insert(iInsert++, toolStripSeparator16);
            menuStrip.Items.Insert(iInsert++, transitionsContextMenuItem);
            // Sometimes child menuitems are stripped from the parent
            if (transitionsContextMenuItem.DropDownItems.Count == 0)
            {
                transitionsContextMenuItem.DropDownItems.AddRange(new ToolStripItem[]
                    {
                        allTranContextMenuItem,
                        precursorsTranContextMenuItem,
                        productsTranContextMenuItem,
                        singleTranContextMenuItem,
                        totalTranContextMenuItem
                    });
            }
            menuStrip.Items.Insert(iInsert++, transformChromContextMenuItem);
            // Sometimes child menuitems are stripped from the parent
            if (transformChromContextMenuItem.DropDownItems.Count == 0)
            {
                transformChromContextMenuItem.DropDownItems.AddRange(new ToolStripItem[]
                    {
                        transformChromNoneContextMenuItem,
                        secondDerivativeContextMenuItem,
                        firstDerivativeContextMenuItem,
                        smoothSGChromContextMenuItem
                    });                
            }
            menuStrip.Items.Insert(iInsert++, toolStripSeparator17);
            menuStrip.Items.Insert(iInsert++, autoZoomContextMenuItem);
            // Sometimes child menuitems are stripped from the parent
            if (autoZoomContextMenuItem.DropDownItems.Count == 0)
            {
                autoZoomContextMenuItem.DropDownItems.AddRange(new ToolStripItem[]
                    {
                        autoZoomNoneContextMenuItem,
                        autoZoomBestPeakContextMenuItem,
                        autoZoomRTWindowContextMenuItem,
                        autoZoomBothContextMenuItem
                    });                
            }
            lockYChromContextMenuItem.Checked = set.LockYChrom;
            menuStrip.Items.Insert(iInsert++, lockYChromContextMenuItem);
            synchronizeZoomingContextMenuItem.Checked = set.AutoZoomAllChromatograms;
            menuStrip.Items.Insert(iInsert++, synchronizeZoomingContextMenuItem);
            menuStrip.Items.Insert(iInsert++, toolStripSeparator18);
            menuStrip.Items.Insert(iInsert++, chromPropsContextMenuItem);
            menuStrip.Items.Insert(iInsert, toolStripSeparator19);

            // Remove some ZedGraph menu items not of interest
            foreach (var item in items)
            {
                string tag = (string)item.Tag;
                if (tag == "set_default" || tag == "show_val")
                    menuStrip.Items.Remove(item);
            }
            CopyEmfToolStripMenuItem.AddToContextMenu(zedGraphControl, menuStrip);
        }

// ReSharper disable SuggestBaseTypeForParameter
        private static bool HasPeak(int iResult, TransitionGroupDocNode nodeGroup)
// ReSharper restore SuggestBaseTypeForParameter
        {
            foreach (TransitionDocNode nodeTran in nodeGroup.Children)
            {
                if (HasPeak(iResult, nodeTran))
                    return true;
            }
            return false;
        }

        private static bool HasPeak(int iResults, TransitionDocNode nodeTran)
        {
            var chromInfo = GetTransitionChromInfo(nodeTran, iResults);
            return (chromInfo != null && !chromInfo.IsEmpty);
        }

        private void legendChromContextMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.ShowChromatogramLegend = legendChromContextMenuItem.Checked;
            UpdateChromGraphs();
        }

        private void peakBoundariesContextMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.ShowPeakBoundaries = peakBoundariesContextMenuItem.Checked;
            UpdateChromGraphs();
        }

        private void retentionTimesContextMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            var showRT = GraphChromatogram.ShowRT;

            allRTContextMenuItem.Checked = (showRT == ShowRTChrom.all);
            bestRTContextMenuItem.Checked = (showRT == ShowRTChrom.best);
            thresholdRTContextMenuItem.Checked = (showRT == ShowRTChrom.threshold);
            noneRTContextMenuItem.Checked = (showRT == ShowRTChrom.none);
        }

        private void allRTContextMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.ShowRetentionTimesEnum = ShowRTChrom.all.ToString();
            UpdateChromGraphs();
        }

        private void bestRTContextMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.ShowRetentionTimesEnum = ShowRTChrom.best.ToString();
            UpdateChromGraphs();
        }

        private void thresholdRTContextMenuItem_Click(object sender, EventArgs e)
        {
            ShowRTThresholdDlg();
        }

        public void ShowRTThresholdDlg()
        {
            using (var dlg = new ShowRTThresholdDlg())
            {
                double threshold = Settings.Default.ShowRetentionTimesThreshold;
                if (threshold > 0)
                    dlg.Threshold = threshold;

                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    Settings.Default.ShowRetentionTimesThreshold = dlg.Threshold;
                    Settings.Default.ShowRetentionTimesEnum = ShowRTChrom.threshold.ToString();
                    UpdateChromGraphs();
                }
            }
        }

        private void noneRTContextMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.ShowRetentionTimesEnum = ShowRTChrom.none.ToString();
            UpdateChromGraphs();
        }

        private void retentionTimePredContextMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.ShowRetentionTimePred = retentionTimePredContextMenuItem.Checked;
            UpdateChromGraphs();
        }

        private void peptideIDTimesContextMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.ShowPeptideIdTimes = peptideIDTimesContextMenuItem.Checked;
            UpdateChromGraphs();
        }

        private void alignedPeptideIDTimesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.ShowAlignedPeptideIdTimes = alignedPeptideIDTimesToolStripMenuItem.Checked;
            UpdateChromGraphs();
        }

        private void nextReplicateMenuItem_Click(object sender, EventArgs e)
        {
            SelectedResultsIndex++;
        }

        private void previousReplicateMenuItem_Click(object sender, EventArgs e)
        {
            SelectedResultsIndex--;
        }

        private void transitionsMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            var displayType = GraphChromatogram.GetDisplayType(DocumentUI);

            // If both MS1 and MS/MS ions are not possible, then menu items to differentiate precursors and
            // products are not necessary.
            var fullScan = DocumentUI.Settings.TransitionSettings.FullScan;
            precursorsTranMenuItem.Visible =
                precursorsTranContextMenuItem.Visible =
                productsTranMenuItem.Visible =
                productsTranContextMenuItem.Visible = fullScan.IsEnabledMs && fullScan.IsEnabledMsMs;

            precursorsTranMenuItem.Checked = precursorsTranContextMenuItem.Checked =
                (displayType == DisplayTypeChrom.precursors);
            productsTranMenuItem.Checked = productsTranContextMenuItem.Checked =
                (displayType == DisplayTypeChrom.products);
            singleTranMenuItem.Checked = singleTranContextMenuItem.Checked =
                (displayType == DisplayTypeChrom.single);
            allTranMenuItem.Checked = allTranContextMenuItem.Checked =
                (displayType == DisplayTypeChrom.all);
            totalTranMenuItem.Checked = totalTranContextMenuItem.Checked =
                (displayType == DisplayTypeChrom.total);
        }

        private void removePeaksGraphMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            ToolStripMenuItem menu = removePeaksGraphMenuItem;

            var nodeGroupTree = SequenceTree.GetNodeOfType<TransitionGroupTreeNode>();
            TransitionGroupDocNode nodeGroup;
            IdentityPath pathGroup;
            if (nodeGroupTree != null)
            {
                nodeGroup = nodeGroupTree.DocNode;
                pathGroup = nodeGroupTree.Path;
            }
            else
            {
                var nodePepTree = SelectedNode as PeptideTreeNode;
                Debug.Assert(nodePepTree != null && nodePepTree.Nodes.Count == 1);  // menu item incorrectly enabled
                if (nodePepTree == null || nodePepTree.Nodes.Count != 1)
                    return;
                nodeGroup = (TransitionGroupDocNode) nodePepTree.DocNode.Children[0];
                pathGroup = new IdentityPath(nodePepTree.Path, nodeGroup.Id);
            }

            int i = 0;
            int iResults = SequenceTree.ResultsIndex;
            foreach (TransitionDocNode nodeTran in nodeGroup.Children)
            {
                var chromInfo = GetTransitionChromInfo(nodeTran, iResults);
                if (chromInfo == null || chromInfo.IsEmpty)
                    continue;

                string name = ChromGraphItem.GetTitle(nodeTran);

                ToolStripMenuItem item = null;
                if (i < menu.DropDownItems.Count)
                    item = menu.DropDownItems[i] as ToolStripMenuItem;
                if (item == null || name != item.Name)
                {
                    // Remove the rest of the existing items
                    while (i < menu.DropDownItems.Count)
                        menu.DropDownItems.RemoveAt(i);

                    RemovePeakHandler handler = new RemovePeakHandler(this, pathGroup, nodeGroup, nodeTran);
                    item = new ToolStripMenuItem(name, null, handler.menuItem_Click);
                    menu.DropDownItems.Insert(i, item);
                }

                i++;
            }

            // Remove the rest of the existing items
            while (i < menu.DropDownItems.Count)
                menu.DropDownItems.RemoveAt(i);

            if (i > 1)
            {
                RemovePeakHandler handler = new RemovePeakHandler(this, pathGroup, nodeGroup, null);
                var item = new ToolStripMenuItem("All", null, handler.menuItem_Click);
                menu.DropDownItems.Insert(i, item);
            }
        }

        private class RemovePeakHandler
        {
            private readonly SkylineWindow _skyline;
            private readonly IdentityPath _groupPath;
            private readonly TransitionGroupDocNode _nodeGroup;
            private readonly TransitionDocNode _nodeTran;

            public RemovePeakHandler(SkylineWindow skyline, IdentityPath groupPath,
                TransitionGroupDocNode nodeGroup, TransitionDocNode nodeTran)
            {
                _skyline = skyline;
                _groupPath = groupPath;
                _nodeGroup = nodeGroup;
                _nodeTran = nodeTran;
            }

            public void menuItem_Click(object sender, EventArgs e)
            {
                _skyline.RemovePeak(_groupPath, _nodeGroup, _nodeTran);
            }
        }

        private void removePeakContextMenuItem_Click(object sender, EventArgs e)
        {
            var nodeTranTree = SelectedNode as TransitionTreeNode;
            if (nodeTranTree == null)
                return;
            var nodeGroup = ((TransitionGroupTreeNode) nodeTranTree.Parent).DocNode;
            var nodeTran = nodeTranTree.DocNode;
            RemovePeak(SelectedPath.Parent, nodeGroup, nodeTran);
        }

        public void RemovePeak(IdentityPath groupPath,
            TransitionGroupDocNode nodeGroup, TransitionDocNode nodeTran)
        {
            string message;
            ChromInfo chromInfo;
            Transition transition;

            int iResults = SequenceTree.ResultsIndex;
            if (nodeTran == null)
            {
                message = string.Format("Remove all peaks from {0}", ChromGraphItem.GetTitle(nodeGroup));
                chromInfo = GetTransitionGroupChromInfo(nodeGroup, iResults);
                transition = null;
            }
            else
            {
                message = string.Format("Remove peak from {0}", ChromGraphItem.GetTitle(nodeTran));
                chromInfo = GetTransitionChromInfo(nodeTran, iResults);
                transition = nodeTran.Transition;
            }
            if (chromInfo == null)
                return;

            string filePath;
            string name = GetGraphChromStrings(iResults, chromInfo.FileId, out filePath);
            if (name == null)
                return;

            ModifyDocument(message,
                doc => doc.ChangePeak(groupPath, name, filePath, transition, 0, 0, false));            
        }

        private static TransitionGroupChromInfo GetTransitionGroupChromInfo(TransitionGroupDocNode nodeGroup, int iResults)
        {
            if (iResults == -1 || !nodeGroup.HasResults || iResults >= nodeGroup.Results.Count)
                return null;
            var listChromInfo = nodeGroup.Results[iResults];
            if (listChromInfo == null)
                return null;
            return listChromInfo[0];
        }

        private static TransitionChromInfo GetTransitionChromInfo(TransitionDocNode nodeTran, int iResults)
        {
            if (iResults == -1 || !nodeTran.HasResults || iResults >= nodeTran.Results.Count)
                return null;
            var listChromInfo = nodeTran.Results[iResults];
            if (listChromInfo == null)
                return null;
            return listChromInfo[0];            
        }

        private void singleTranMenuItem_Click(object sender, EventArgs e)
        {
            ShowSingleTransition();
        }

        public void ShowSingleTransition()
        {
            SetDisplayTypeChrom(DisplayTypeChrom.single);
        }

        private void precursorsTranMenuItem_Click(object sender, EventArgs e)
        {
            ShowPrecursorTransitions();
        }

        public void ShowPrecursorTransitions()
        {
            SetDisplayTypeChrom(DisplayTypeChrom.precursors);
        }

        private void productsTranMenuItem_Click(object sender, EventArgs e)
        {
            ShowProductTransitions();
        }

        public void ShowProductTransitions()
        {
            SetDisplayTypeChrom(DisplayTypeChrom.products);
        }

        private void allTranMenuItem_Click(object sender, EventArgs e)
        {
            ShowAllTransitions();
        }

        public void ShowAllTransitions()
        {
            SetDisplayTypeChrom(DisplayTypeChrom.all);
        }

        private void totalTranMenuItem_Click(object sender, EventArgs e)
        {
            ShowTotalTransitions();
        }

        public void ShowTotalTransitions()
        {
            SetDisplayTypeChrom(DisplayTypeChrom.total);
        }

        public void SetDisplayTypeChrom(DisplayTypeChrom displayType)
        {
            Settings.Default.ShowTransitionGraphs = displayType.ToString();
            UpdateChromGraphs();
            UpdateRetentionTimeGraph();
            UpdatePeakAreaGraph();
        }

        private void transformChromMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            var transform = GraphChromatogram.Transform;

            transformChromNoneMenuItem.Checked = transformChromNoneContextMenuItem.Checked =
                (transform == TransformChrom.none);
            secondDerivativeMenuItem.Checked = secondDerivativeContextMenuItem.Checked =
                (transform == TransformChrom.craw2d);
            firstDerivativeMenuItem.Checked = firstDerivativeContextMenuItem.Checked =
                (transform == TransformChrom.craw1d);
            smoothSGChromMenuItem.Checked = smoothSGChromContextMenuItem.Checked =
                (transform == TransformChrom.savitzky_golay);
        }

        private void transformChromNoneMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.TransformTypeChromatogram = TransformChrom.none.ToString();
            UpdateChromGraphs();
        }

        private void secondDerivativeMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.TransformTypeChromatogram = TransformChrom.craw2d.ToString();
            UpdateChromGraphs();
        }

        private void firstDerivativeMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.TransformTypeChromatogram = TransformChrom.craw1d.ToString();
            UpdateChromGraphs();
        }

        private void smoothSGChromMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.TransformTypeChromatogram = TransformChrom.savitzky_golay.ToString();
            UpdateChromGraphs();
        }

        private void lockYChromContextMenuItem_Click(object sender, EventArgs e)
        {
            bool lockY = Settings.Default.LockYChrom = lockYChromContextMenuItem.Checked;
            // Avoid updating the rest of the chart just to change the y-axis lock state
            foreach (var chromatogram in _listGraphChrom)
                chromatogram.LockYAxis(lockY);
        }

        private void synchronizeZoomingContextMenuItem_Click(object sender, EventArgs e)
        {
            bool zoomAll = Settings.Default.AutoZoomAllChromatograms = synchronizeZoomingContextMenuItem.Checked;

            if (zoomAll)
            {
                var activeForm = dockPanel.ActiveContent;
                int iActive = _listGraphChrom.IndexOf(chrom => ReferenceEquals(chrom, activeForm));
                ZoomState zoomState = (iActive != -1 ? _listGraphChrom[iActive].ZoomState : null);
                if (zoomState != null)
                    graphChromatogram_ZoomAll(null, new ZoomEventArgs(zoomState));
            }
        }

        private void autozoomMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            bool hasRt = (DocumentUI.Settings.PeptideSettings.Prediction.RetentionTime != null);
            autoZoomRTWindowMenuItem.Enabled = autoZoomRTWindowContextMenuItem.Enabled = hasRt;
            autoZoomBothMenuItem.Enabled = autoZoomBothContextMenuItem.Enabled = hasRt;

            var zoom = GraphChromatogram.AutoZoom;
            if (!hasRt)
            {
                if (zoom == AutoZoomChrom.window)
                    zoom = AutoZoomChrom.none;
                else if (zoom == AutoZoomChrom.both)
                    zoom = AutoZoomChrom.peak;
            }

            autoZoomNoneMenuItem.Checked = autoZoomNoneContextMenuItem.Checked =
                (zoom == AutoZoomChrom.none);
            autoZoomBestPeakMenuItem.Checked = autoZoomBestPeakContextMenuItem.Checked =
                (zoom == AutoZoomChrom.peak);
            autoZoomRTWindowMenuItem.Checked = autoZoomRTWindowContextMenuItem.Checked =
                (zoom == AutoZoomChrom.window);
            autoZoomBothMenuItem.Checked = autoZoomBothContextMenuItem.Checked =
                (zoom == AutoZoomChrom.both);
        }

        private void autoZoomNoneMenuItem_Click(object sender, EventArgs e)
        {
            AutoZoomNone();
        }

        public void AutoZoomNone()
        {
            Settings.Default.AutoZoomChromatogram = AutoZoomChrom.none.ToString();
            UpdateChromGraphs();
        }

        private void autoZoomBestPeakMenuItem_Click(object sender, EventArgs e)
        {
            AutoZoomBestPeak();
        }

        public void AutoZoomBestPeak()
        {
            Settings.Default.AutoZoomChromatogram = AutoZoomChrom.peak.ToString();
            UpdateChromGraphs();
        }

        private void autoZoomRTWindowMenuItem_Click(object sender, EventArgs e)
        {
            AutoZoomRTWindow();
        }

        public void AutoZoomRTWindow()
        {
            Settings.Default.AutoZoomChromatogram = AutoZoomChrom.window.ToString();
            UpdateChromGraphs();
        }

        private void autoZoomBothMenuItem_Click(object sender, EventArgs e)
        {
            AutoZoomBoth();
        }

        public void AutoZoomBoth()
        {
            Settings.Default.AutoZoomChromatogram = AutoZoomChrom.both.ToString();
            UpdateChromGraphs();
        }

        private void chromPropsContextMenuItem_Click(object sender, EventArgs e)
        {
            using (var dlg = new ChromChartPropertyDlg())
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    UpdateChromGraphs();
            }
        }

        private void ShowGraphChrom(string name, bool show)
        {
            var graphChrom = GetGraphChrom(name);
            if (graphChrom != null)
            {
                if (show)
                {
                    graphChrom.Activate();
                    graphChrom.Focus();
                }
                else
                    graphChrom.Hide();
            }
            else if (show)
            {
                CreateGraphChrom(name, SelectedGraphChromName, false);
            }
        }

        public IEnumerable<GraphChromatogram> GraphChromatograms { get { return _listGraphChrom; } }

        public GraphChromatogram GetGraphChrom(string name)
        {
            int iGraph = _listGraphChrom.IndexOf(graph => Equals(graph.NameSet, name));
            return (iGraph != -1 ? _listGraphChrom[iGraph] : null);
        }

//        private bool IsGraphChromVisible(string name)
//        {
//            int iGraph = _listGraphChrom.IndexOf(graph => Equals(graph.NameSet, name));
//            return iGraph != -1 && !_listGraphChrom[iGraph].IsHidden;
//        }

        private string SelectedGraphChromName
        {
            get
            {
                string temp;
                return GetGraphChromStrings(SelectedResultsIndex, null, out temp);
            }
        }

        private string GetGraphChromStrings(int iResult, ChromFileInfoId fileId, out string filePath)
        {
            filePath = null;
            if (iResult != -1)
            {
                var settings = DocumentUI.Settings;
                if (settings.HasResults && iResult < settings.MeasuredResults.Chromatograms.Count)
                {
                    var chromatogramSet = settings.MeasuredResults.Chromatograms[iResult];
                    if (fileId != null)
                        filePath = chromatogramSet.GetFileInfo(fileId).FilePath;
                    return chromatogramSet.Name;                    
                }
            }
            return null;
        }

        private GraphChromatogram CreateGraphChrom(string name)
        {
            var graphChrom = new GraphChromatogram(name, this);
            graphChrom.FormClosed += graphChromatogram_FormClosed;
            graphChrom.PickedPeak += graphChromatogram_PickedPeak;
            graphChrom.ChangedPeakBounds += graphChromatogram_ChangedPeakBounds;
            graphChrom.PickedSpectrum += graphChromatogram_PickedSpectrum;
            graphChrom.ZoomAll += graphChromatogram_ZoomAll;
            _listGraphChrom.Add(graphChrom);
            return graphChrom;
        }

        private void DestroyGraphChrom(GraphChromatogram graphChrom)
        {
            // Detach event handlers and dispose
            graphChrom.FormClosed -= graphChromatogram_FormClosed;
            graphChrom.PickedPeak -= graphChromatogram_PickedPeak;
            graphChrom.ChangedPeakBounds -= graphChromatogram_ChangedPeakBounds;
            graphChrom.PickedSpectrum -= graphChromatogram_PickedSpectrum;
            graphChrom.ZoomAll -= graphChromatogram_ZoomAll;
            graphChrom.Close();
        }

        private void CreateGraphChrom(string name, string namePosition, bool split)
        {
            // Create a new spectrum graph
            var graphChrom = CreateGraphChrom(name);
            int firstDocumentPane = FirstDocumentPane;
            if (firstDocumentPane == -1)
                graphChrom.Show(dockPanel, DockState.Document);
            else
            {
                var graphPosition = GetGraphChrom(namePosition);

                IDockableForm formBefore;
                DockPane paneExisting = FindChromatogramPane(graphPosition, out formBefore);
                if (paneExisting == null)
                    graphChrom.Show(dockPanel.Panes[firstDocumentPane], DockPaneAlignment.Left, 0.5);
                else if (!split)
                {
                    graphChrom.Show(paneExisting, paneExisting.Contents[0]);
                }
                else
                {
                    var alignment = (graphChrom.Width > graphChrom.Height ?
                        DockPaneAlignment.Right : DockPaneAlignment.Bottom);
                    graphChrom.Show(paneExisting, alignment, 0.5);
                }
            }
        }

        private int FirstDocumentPane
        {
            get
            {
                return dockPanel.Panes.IndexOf(pane => !pane.IsHidden && pane.DockState == DockState.Document);
            }
        }

        private DockPane FindChromatogramPane(GraphChromatogram graphChrom, out IDockableForm formBefore)
        {
            foreach (var pane in dockPanel.Panes)
            {
                foreach (IDockableForm form in pane.Contents)
                {
                    if (form is GraphChromatogram &&
                        (graphChrom == null || graphChrom == form))
                    {
                        formBefore = form;
                        return pane;
                    }
                }
            }
            formBefore = null;
            return null;
        }

        private DockPane FindPane(IDockableForm dockableForm)
        {
            // Floating panes may be created but hidden for windows that allow floating
            int iPane = dockPanel.Panes.IndexOf(pane => !pane.IsHidden && pane.Contents.Contains(dockableForm));
            return (iPane != -1 ? dockPanel.Panes[iPane] : null);
        }

        private void graphChromatogram_FormClosed(object sender, FormClosedEventArgs e)
        {
            _listGraphChrom.Remove((GraphChromatogram)sender);
        }

        private void graphChromatogram_PickedPeak(object sender, PickedPeakEventArgs e)
        {
            var graphChrom = sender as GraphChromatogram;
            if (graphChrom != null)
                graphChrom.LockZoom();
            try
            {
                ModifyDocument(string.Format("Pick peak {0:F01}", e.RetentionTime),
                    doc => doc.ChangePeak(e.GroupPath, e.NameSet, e.FilePath, e.TransitionId, e.RetentionTime));
            }
            finally
            {
                if (graphChrom != null)
                    graphChrom.UnlockZoom();
            }
        }

        private void graphChromatogram_ChangedPeakBounds(object sender, ChangedMultiPeakBoundsEventArgs eMulti)
        {
            var graphChrom = sender as GraphChromatogram;
            if (graphChrom != null)
                graphChrom.LockZoom();
            try
            {
                // Handle most common case of a change to a single group first.
                if (eMulti.Changes.Length == 1)
                {
                    string message;
                    ChangedPeakBoundsEventArgs e = eMulti.Changes[0];
                    if (e.StartTime == e.EndTime)
                        message = "Remove peak";
                    else if (e.ChangeType == PeakBoundsChangeType.both)
                        message = string.Format("Change peak to {0:F01}-{1:F01}", e.StartTime, e.EndTime);
                    else if (e.ChangeType == PeakBoundsChangeType.start)
                        message = string.Format("Change peak start to {0:F01}", e.StartTime);
                    else
                        message = string.Format("Change peak end to {0:F01}", e.EndTime);
                    ModifyDocument(message, doc => doc.ChangePeak(e.GroupPath, e.NameSet, e.FilePath, e.Transition,
                                                                  e.StartTime, e.EndTime, e.IsIndentified));
                }
                else
                {
                    ModifyDocument("Change peaks",
                        doc =>
                            {
                                foreach (var e in eMulti.Changes)
                                {
                                    doc = doc.ChangePeak(e.GroupPath, e.NameSet, e.FilePath, e.Transition,
                                        e.StartTime, e.EndTime, e.IsIndentified);                                    
                                }
                                return doc;
                            });
                }
            }
            finally
            {
                if (graphChrom != null)
                    graphChrom.UnlockZoom();
            }
        }

        private void graphChromatogram_PickedSpectrum(object sender, PickedSpectrumEventArgs e)
        {
            if (_graphSpectrum != null)
                _graphSpectrum.SelectSpectrum(e.SpectrumId);
        }

        private void graphChromatogram_ZoomAll(object sender, ZoomEventArgs e)
        {
            foreach (var graphChrom in _listGraphChrom)
            {
                if (!ReferenceEquals(sender, graphChrom))
                {
                    graphChrom.ZoomTo(e.ZoomState);
                    graphChrom.UpdateUI();
                }
            }
        }

        private void UpdateChromGraphs()
        {
            foreach (var graphChrom in _listGraphChrom)
                graphChrom.UpdateUI();
        }

        private void closeAllChromatogramsMenuItem_Click(object sender, EventArgs e)
        {
            foreach (var graphChromatogram in _listGraphChrom)
            {
                graphChromatogram.Hide();
            }
        }

        #endregion

        #region Retention time graph

        public GraphSummary GraphRetentionTime { get { return _graphRetentionTime; } }

        public void ShowGraphRetentionTime(bool show)
        {
            if (show)
            {
                if (_graphRetentionTime != null)
                {
                    _graphRetentionTime.Activate();
                }
                else
                {
                    _graphRetentionTime = CreateGraphRetentionTime();

                    // Choose a position to float the window
                    var rectFloat = dockPanel.Bounds;
                    rectFloat = dockPanel.RectangleToScreen(rectFloat);
                    rectFloat.X += rectFloat.Width / 4;
                    rectFloat.Y += rectFloat.Height / 3;
                    rectFloat.Width = Math.Max(600, rectFloat.Width / 2);
                    rectFloat.Height = Math.Max(440, rectFloat.Height / 2);
                    // Make sure it is on the screen.
                    var screen = Screen.FromControl(dockPanel);
                    var rectScreen = screen.WorkingArea;
                    rectFloat.X = Math.Max(rectScreen.X, Math.Min(rectScreen.Width - rectFloat.Width, rectFloat.X));
                    rectFloat.Y = Math.Max(rectScreen.Y, Math.Min(rectScreen.Height - rectFloat.Height, rectFloat.Y));

                    _graphRetentionTime.Show(dockPanel, rectFloat);
                }
            }
            else if (_graphRetentionTime != null)
            {
                // Save current setting for showing spectra
                show = Settings.Default.ShowRetentionTimeGraph;
                // Close the spectrum graph window
                _graphRetentionTime.Hide();
                // Restore setting and menuitem from saved value
                Settings.Default.ShowRetentionTimeGraph = show;
            }
        }

        private GraphSummary CreateGraphRetentionTime()
        {
            _graphRetentionTime = new GraphSummary(this, new RTGraphController()) {TabText = "Retention Times"};
            _graphRetentionTime.FormClosed += graphRetentinTime_FormClosed;
            _graphRetentionTime.VisibleChanged += graphRetentionTime_VisibleChanged;
            return _graphRetentionTime;
        }

        private void DestroyGraphRetentionTime()
        {
            if (_graphRetentionTime != null)
            {
                _graphRetentionTime.FormClosed -= graphRetentinTime_FormClosed;
                _graphRetentionTime.VisibleChanged -= graphRetentionTime_VisibleChanged;
                _graphRetentionTime.Close();
                _graphRetentionTime = null;
            }
        }

        private void graphRetentionTime_VisibleChanged(object sender, EventArgs e)
        {
            Settings.Default.ShowRetentionTimeGraph =
                (_graphRetentionTime != null && _graphRetentionTime.Visible);
        }

        private void graphRetentinTime_FormClosed(object sender, FormClosedEventArgs e)
        {
            // Update settings and menu check
            Settings.Default.ShowRetentionTimeGraph = false;

            _graphRetentionTime = null;
        }

        void GraphSummary.IStateProvider.BuildGraphMenu(ZedGraphControl zedGraphControl, ContextMenuStrip menuStrip, Point mousePt,
            GraphSummary.IController controller)
        {
            if (controller is RTGraphController)
                BuildRTGraphMenu(menuStrip, mousePt, (RTGraphController) controller);
            else if (controller is AreaGraphController)
                BuildAreaGraphMenu(menuStrip);
            CopyEmfToolStripMenuItem.AddToContextMenu(zedGraphControl, menuStrip);
        }

        public TreeNodeMS SelectedNode
        {
            get { return SequenceTree != null ? SequenceTree.SelectedNode as TreeNodeMS : null; }
        }

        public IdentityPath SelectedPath
        {
            get { return SequenceTree != null ? SequenceTree.SelectedPath : new IdentityPath(); }
            set { SequenceTree.SelectedPath = value; }
        }

        public IList<TreeNodeMS> SelectedNodes
        {
            get { return SequenceTree != null ? SequenceTree.SelectedNodes.ToArray() : new TreeNodeMS[0]; }
        }

        public int SelectedResultsIndex
        {
            get { return ComboResults != null ? ComboResults.SelectedIndex : -1; }
            set
            {
                if (0 <= value && value < ComboResults.Items.Count)
                {
                    var focusStart = User32.GetFocusedControl();
                    ComboResults.SelectedIndex = value;
                    if (focusStart != null)
                        focusStart.Focus();
                }
            }
        }

        public void ActivateReplicate(string name)
        {
            int index;
            ChromatogramSet chromatogramSet;

            if (DocumentUI.Settings.MeasuredResults.TryGetChromatogramSet(name, out chromatogramSet, out index))
            {
                SelectedResultsIndex = index;
            }
        }

        public SpectrumDisplayInfo SelectedSpectrum
        {
            get { return _graphSpectrum != null ? _graphSpectrum.SelectedSpectrum : null; }
        }

        public void ActivateSpectrum()
        {
            ShowGraphSpectrum(true);
        }

        private void BuildRTGraphMenu(ToolStrip menuStrip, Point mousePt,
            RTGraphController controller)
        {
            // Store original menuitems in an array, and insert a separator
            ToolStripItem[] items = new ToolStripItem[menuStrip.Items.Count];
            int iUnzoom = -1;
            for (int i = 0; i < items.Length; i++)
            {
                items[i] = menuStrip.Items[i];
                string tag = (string)items[i].Tag;
                if (tag == "unzoom")
                    iUnzoom = i;
            }

            if (iUnzoom != -1)
                menuStrip.Items.Insert(iUnzoom, toolStripSeparator25);

            // Insert skyline specific menus
            var set = Settings.Default;
            int iInsert = 0;
            menuStrip.Items.Insert(iInsert++, timeGraphContextMenuItem);
            if (timeGraphContextMenuItem.DropDownItems.Count == 0)
            {
                timeGraphContextMenuItem.DropDownItems.AddRange(new ToolStripItem[]
                {
                    replicateComparisonContextMenuItem,
                    timePeptideComparisonContextMenuItem,
                    linearRegressionContextMenuItem,
                    schedulingContextMenuItem
                });
            }

            GraphTypeRT graphType = RTGraphController.GraphType;
            if (graphType == GraphTypeRT.regression)
            {
                refineRTContextMenuItem.Checked = set.RTRefinePeptides;
                menuStrip.Items.Insert(iInsert++, refineRTContextMenuItem);
                predictionRTContextMenuItem.Checked = set.RTPredictorVisible;
                menuStrip.Items.Insert(iInsert++, predictionRTContextMenuItem);
                if (DocumentUI.Settings.HasResults && 
                    DocumentUI.Settings.MeasuredResults.Chromatograms.Count > 1)
                {
                    menuStrip.Items.Insert(iInsert++, replicatesRTContextMenuItem);
                    if (replicatesRTContextMenuItem.DropDownItems.Count == 0)
                    {
                        replicatesRTContextMenuItem.DropDownItems.AddRange(new ToolStripItem[]
                        {
                            averageReplicatesContextMenuItem,
                            singleReplicateRTContextMenuItem,
                            bestReplicateRTContextMenuItem
                        });
                    }
                }
                menuStrip.Items.Insert(iInsert++, setRTThresholdContextMenuItem);
                menuStrip.Items.Insert(iInsert++, toolStripSeparator22);
                menuStrip.Items.Insert(iInsert++, createRTRegressionContextMenuItem);
                menuStrip.Items.Insert(iInsert++, chooseCalculatorContextMenuItem);
                if (chooseCalculatorContextMenuItem.DropDownItems.Count == 0)
                {
                    chooseCalculatorContextMenuItem.DropDownItems.AddRange(new ToolStripItem[]
                    {
                        placeholderToolStripMenuItem1,
                        toolStripSeparatorCalculators,
                        addCalculatorContextMenuItem,
                        updateCalculatorContextMenuItem
                    });
                }
                var regressionRT = RTGraphController.RegressionRefined;
                createRTRegressionContextMenuItem.Enabled = (regressionRT != null);
                updateCalculatorContextMenuItem.Visible = (regressionRT != null &&
                    Settings.Default.RTScoreCalculatorList.CanEditItem(regressionRT.Calculator));
                bool showDelete = controller.ShowDelete(mousePt);
                bool showDeleteOutliers = controller.ShowDeleteOutliers;
                if (showDelete || showDeleteOutliers)
                {
                    menuStrip.Items.Insert(iInsert++, toolStripSeparator23);
                    if (showDelete)
                        menuStrip.Items.Insert(iInsert++, removeRTContextMenuItem);
                    if (showDeleteOutliers)
                        menuStrip.Items.Insert(iInsert++, removeRTOutliersContextMenuItem);
                }
            }
            else if (graphType == GraphTypeRT.schedule)
            {
                menuStrip.Items.Insert(iInsert++, toolStripSeparator38);
                menuStrip.Items.Insert(iInsert++, timePropsContextMenuItem);                
            }
            else
            {
                menuStrip.Items.Insert(iInsert++, toolStripSeparator16);
                if (graphType == GraphTypeRT.peptide)
                {
                    menuStrip.Items.Insert(iInsert++, peptideRTValueMenuItem);
                    if (peptideRTValueMenuItem.DropDownItems.Count == 0)
                    {
                        peptideRTValueMenuItem.DropDownItems.AddRange(new ToolStripItem[]
                        {
                            allRTValueContextMenuItem,
                            timeRTValueContextMenuItem,
                            fwhmRTValueContextMenuItem,
                            fwbRTValueContextMenuItem
                        });
                    }
                }
                menuStrip.Items.Insert(iInsert++, transitionsContextMenuItem);
                // Sometimes child menuitems are stripped from the parent
                if (transitionsContextMenuItem.DropDownItems.Count == 0)
                {
                    transitionsContextMenuItem.DropDownItems.AddRange(new ToolStripItem[]
                    {
                        allTranContextMenuItem,
                        precursorsTranContextMenuItem,
                        productsTranContextMenuItem,
                        singleTranContextMenuItem,
                        totalTranContextMenuItem
                    });
                }
                if (graphType == GraphTypeRT.replicate)
                {
                    menuStrip.Items.Insert(iInsert++, replicateOrderContextMenuItem);
                    if (replicateOrderContextMenuItem.DropDownItems.Count == 0)
                    {
                        replicateOrderContextMenuItem.DropDownItems.AddRange(new ToolStripItem[]
                        {
                            replicateOrderDocumentContextMenuItem,
                            replicateOrderAcqTimeContextMenuItem
                        });
                    }
                    var rtReplicateGraphPane = _graphRetentionTime.GraphPane as RTReplicateGraphPane;
                    if (rtReplicateGraphPane != null && rtReplicateGraphPane.CanShowRTLegend)
                    {
                        showRTLegendContextMenuItem.Checked = set.ShowRetentionTimesLegend;
                        menuStrip.Items.Insert(iInsert++, showRTLegendContextMenuItem);
                    }
                    if (rtReplicateGraphPane != null)
                    {
                        if (DocumentUI.Settings.HasResults && !Document.Settings.DocumentRetentionTimes.FileAlignments.IsEmpty)
                        {
                            var alignToSelectionItem = alignRTToSelectionContextMenuItem;
                            alignToSelectionItem.Text = string.Format("Align Times To {0}", ComboResults.SelectedItem);
                            alignToSelectionItem.Checked = AlignToReplicate == SelectedResultsIndex;
                            menuStrip.Items.Insert(iInsert++, alignToSelectionItem);
                        }
                    }
                }
                else if (graphType == GraphTypeRT.peptide)
                {
                    menuStrip.Items.Insert(iInsert++, peptideOrderContextMenuItem);
                    if (peptideOrderContextMenuItem.DropDownItems.Count == 0)
                    {
                        peptideOrderContextMenuItem.DropDownItems.AddRange(new ToolStripItem[]
                        {
                            peptideOrderDocumentContextMenuItem,
                            peptideOrderRTContextMenuItem,
                            peptideOrderAreaContextMenuItem
                        });
                    }

                    if (DocumentUI.Settings.HasResults &&
                    DocumentUI.Settings.MeasuredResults.Chromatograms.Count > 1)
                    {
                        menuStrip.Items.Insert(iInsert++, replicatesRTContextMenuItem);
                        if (replicatesRTContextMenuItem.DropDownItems.Count == 0)
                        {
                            replicatesRTContextMenuItem.DropDownItems.AddRange(new ToolStripItem[]
                        {
                            averageReplicatesContextMenuItem,
                            singleReplicateRTContextMenuItem,
                            bestReplicateRTContextMenuItem
                        });
                        }
                    }
                    menuStrip.Items.Insert(iInsert++, scopeContextMenuItem);
                    if (scopeContextMenuItem.DropDownItems.Count == 0)
                    {
                        scopeContextMenuItem.DropDownItems.AddRange(new ToolStripItem[]
                        {
                            documentScopeContextMenuItem,
                            proteinScopeContextMenuItem
                        });
                    }
                    menuStrip.Items.Insert(iInsert++, peptideCvsContextMenuItem);
                    peptideCvsContextMenuItem.Checked = set.ShowPeptideCV;                    
                }
                selectionContextMenuItem.Checked = set.ShowReplicateSelection;
                menuStrip.Items.Insert(iInsert++, selectionContextMenuItem);
                menuStrip.Items.Insert(iInsert++, toolStripSeparator38);
                menuStrip.Items.Insert(iInsert++, timePropsContextMenuItem);
            }

            menuStrip.Items.Insert(iInsert, toolStripSeparator24);

            // Remove some ZedGraph menu items not of interest
            foreach (var item in items)
            {
                string tag = (string)item.Tag;
                if (tag == "set_default" || tag == "show_val")
                    menuStrip.Items.Remove(item);
            }
        }

        private void timeGraphMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            GraphTypeRT graphType = RTGraphController.GraphType;
            linearRegressionMenuItem.Checked = linearRegressionContextMenuItem.Checked = (graphType == GraphTypeRT.regression);
            replicateComparisonMenuItem.Checked = replicateComparisonContextMenuItem.Checked = (graphType == GraphTypeRT.replicate);
            timePeptideComparisonMenuItem.Checked = timePeptideComparisonContextMenuItem.Checked = (graphType == GraphTypeRT.peptide);
            schedulingMenuItem.Checked = schedulingContextMenuItem.Checked = (graphType == GraphTypeRT.schedule);
        }

        private void linearRegressionMenuItem_Click(object sender, EventArgs e)
        {
            ShowRTLinearRegressionGraph();
        }

        public void ShowRTLinearRegressionGraph()
        {
            Settings.Default.RTGraphType = GraphTypeRT.regression.ToString();
            ShowGraphRetentionTime(true);
            UpdateRetentionTimeGraph();
        }

        private void timePeptideComparisonMenuItem_Click(object sender, EventArgs e)
        {
            ShowRTPeptideGraph();
        }

        public void ShowRTPeptideGraph()
        {
            Settings.Default.RTGraphType = GraphTypeRT.peptide.ToString();
            ShowGraphRetentionTime(true);
            UpdateRetentionTimeGraph();
        }

        private void showRTLegendContextMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.ShowRetentionTimesLegend = !Settings.Default.ShowRetentionTimesLegend;
            UpdateRetentionTimeGraph();
        }

        private void replicateComparisonMenuItem_Click(object sender, EventArgs e)
        {
            ShowRTReplicateGraph();
        }

        public void ShowRTReplicateGraph()
        {
            Settings.Default.RTGraphType = GraphTypeRT.replicate.ToString();
            ShowGraphRetentionTime(true);
            UpdateRetentionTimeGraph();
        }

        private void schedulingMenuItem_Click(object sender, EventArgs e)
        {
            ShowRTSchedulingGraph();
        }

        public void ShowRTSchedulingGraph()
        {
            Settings.Default.RTGraphType = GraphTypeRT.schedule.ToString();
            ShowGraphRetentionTime(true);
            UpdateRetentionTimeGraph();
        }

        private void selectionContextMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.ShowReplicateSelection = selectionContextMenuItem.Checked;
            UpdateSummaryGraphs();
        }

        private void refineRTContextMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.RTRefinePeptides = refineRTContextMenuItem.Checked;
            UpdateRetentionTimeGraph();
        }

        private void predictionRTContextMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.RTPredictorVisible = predictionRTContextMenuItem.Checked;
            UpdateRetentionTimeGraph();
        }

        private void averageReplicatesContextMenuItem_Click(object sender, EventArgs e)
        {
            ShowAverageReplicates();
        }

        public void ShowAverageReplicates()
        {
            Settings.Default.ShowRegressionReplicateEnum = ReplicateDisplay.all.ToString();
            UpdateSummaryGraphs();
        }

        private void singleReplicateRTContextMenuItem_Click(object sender, EventArgs e)
        {
            ShowSingleReplicate();
        }

        public void ShowSingleReplicate()
        {
            Settings.Default.ShowRegressionReplicateEnum = ReplicateDisplay.single.ToString();
            // No CVs with single replicate data views
            Settings.Default.ShowPeptideCV = false;
            UpdateSummaryGraphs();
        }

        private void bestReplicateRTContextMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.ShowRegressionReplicateEnum = ReplicateDisplay.best.ToString();
            // No CVs with single replicate data views
            Settings.Default.ShowPeptideCV = false;
            UpdateSummaryGraphs();
        }

        private void replicatesRTContextMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            ReplicateDisplay replicate = RTLinearRegressionGraphPane.ShowReplicate;
            averageReplicatesContextMenuItem.Checked = (replicate == ReplicateDisplay.all);
            singleReplicateRTContextMenuItem.Checked = (replicate == ReplicateDisplay.single);
            bestReplicateRTContextMenuItem.Checked = (replicate == ReplicateDisplay.best);
        }

        private void setRTThresholdContextMenuItem_Click(object sender, EventArgs e)
        {
            ShowSetRTThresholdDlg();
        }

        public void ShowSetRTThresholdDlg()
        {
            using (var dlg = new SetRTThresholdDlg {Threshold = Settings.Default.RTResidualRThreshold})
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    Settings.Default.RTResidualRThreshold = dlg.Threshold;
                    UpdateRetentionTimeGraph();
                }
            }
        }

        private void createRTRegressionContextMenuItem_Click(object sender, EventArgs e)
        {
            CreateRegression();               
        }

        public void CreateRegression()
        {
            if (_graphRetentionTime == null)
                return;

            var listRegression = Settings.Default.RetentionTimeList;
            var regression = RTGraphController.RegressionRefined;
            string name = Path.GetFileNameWithoutExtension(DocumentFilePath);
            if (listRegression.ContainsKey(name))
            {
                int i = 2;
                while (listRegression.ContainsKey(name + i))
                    i++;
                name += i;
            }
            if (regression != null)
                regression = (RetentionTimeRegression) regression.ChangeName(name);

            using (var dlg = new EditRTDlg(listRegression) { Regression = regression })
            {
                dlg.ShowPeptides(true);
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    regression = dlg.Regression;
                    listRegression.Add(regression);

                    ModifyDocument(string.Format("Set regression {0}", regression.Name),
                                   doc =>
                                   doc.ChangeSettings(
                                       doc.Settings.ChangePeptidePrediction(p => p.ChangeRetentionTime(regression))));
                }
            }
        }

        private void chooseCalculatorContextMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            SetupCalculatorChooser();
        }

        public void SetupCalculatorChooser()
        {
            while (!ReferenceEquals(chooseCalculatorContextMenuItem.DropDownItems[0], toolStripSeparatorCalculators))
                chooseCalculatorContextMenuItem.DropDownItems.RemoveAt(0);

            //If no calculator has been picked for use in the graph, get the best one.
            var autoItem = new ToolStripMenuItem("Auto", null, delegate { ChooseCalculator(""); })
                               {
                                   Checked = string.IsNullOrEmpty(Settings.Default.RTCalculatorName)
                               };
            chooseCalculatorContextMenuItem.DropDownItems.Insert(0, autoItem);

            int i = 0;
            foreach (var calculator in Settings.Default.RTScoreCalculatorList)
            {
                string calculatorName = calculator.Name;
                var menuItem = new ToolStripMenuItem(calculatorName, null, delegate { ChooseCalculator(calculatorName);})
                {
                    Checked = Equals(calculatorName, Settings.Default.RTCalculatorName)
                };
                chooseCalculatorContextMenuItem.DropDownItems.Insert(i++, menuItem);
            }
        }

        public void ChooseCalculator(string calculatorName)
        {
            Settings.Default.RTCalculatorName = calculatorName;
            UpdateRetentionTimeGraph();
        }

        private void addCalculatorContextMenuItem_Click(object sender, EventArgs e)
        {
            var list = Settings.Default.RTScoreCalculatorList;
            var calcNew = list.EditItem(this, null, list, null);
            if (calcNew != null)
                list.SetValue(calcNew);
        }

        private void updateCalculatorContextMenuItem_Click(object sender, EventArgs e)
        {
            ShowEditCalculatorDlg();
        }

        public void ShowEditCalculatorDlg()
        {
            var list = Settings.Default.RTScoreCalculatorList;
            var regressionRT = RTGraphController.RegressionRefined;
            if (regressionRT != null && list.CanEditItem(regressionRT.Calculator))
            {
                var calcOld = regressionRT.Calculator;
                var calcNew = list.EditItem(this, calcOld, list, null);
                if (calcNew != null && !Equals(calcNew, calcOld))
                {
                    list.SetValue(calcNew);

                    var regressionRTDoc = DocumentUI.Settings.PeptideSettings.Prediction.RetentionTime;
                    if (regressionRTDoc != null && Equals(calcOld.Name, regressionRTDoc.Calculator.Name))
                    {
                        ModifyDocument(string.Format("Update {0} calculator", calcNew.Name), doc =>
                            doc.ChangeSettings(doc.Settings.ChangePeptidePrediction(predict =>
                                predict.ChangeRetentionTime(predict.RetentionTime.ChangeCalculator(calcNew)))));
                    }
                }
            }
        }

        private void removeRTOutliersContextMenuItem_Click(object sender, EventArgs e)
        {
            RemoveRTOutliers();
        }

        public void RemoveRTOutliers()
        {
            if (_graphRetentionTime == null)
                return;

            var outliers = RTGraphController.Outliers;
            var outlierIds = new HashSet<int>();
            foreach (var outlier in outliers)
                outlierIds.Add(outlier.Id.GlobalIndex);

            ModifyDocument("Remove retention time outliers",
                           doc => (SrmDocument) doc.RemoveAll(outlierIds));
        }

        private void removeRTContextMenuItem_Click(object sender, EventArgs e)
        {
            deleteMenuItem_Click(sender, e);
        }

        private void peptideRTValueMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            RTPeptideValue rtValue = RTPeptideGraphPane.RTValue;
            allRTValueContextMenuItem.Checked = (rtValue == RTPeptideValue.All);
            timeRTValueContextMenuItem.Checked = (rtValue == RTPeptideValue.Retention);
            fwhmRTValueContextMenuItem.Checked = (rtValue == RTPeptideValue.FWHM);
            fwbRTValueContextMenuItem.Checked = (rtValue == RTPeptideValue.FWB);
        }

        private void alignRTToSelectionContextMenuItem_Click(object sender, EventArgs e)
        {
            if (_graphRetentionTime == null)
            {
                return;
            }
            var menuItem = (ToolStripMenuItem) sender;
            var rtReplicateGraphPane = _graphRetentionTime.GraphPane as RTReplicateGraphPane;
            if (rtReplicateGraphPane != null)
            {
                if (menuItem.Checked)
                {
                    AlignToReplicate = SelectedResultsIndex;    
                }
                else
                {
                    AlignToReplicate = -1;
                }
            }
        }

        private void allRTValueContextMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.RTPeptideValue = RTPeptideValue.All.ToString();
            // No CVs with all retention time values showing
            Settings.Default.ShowPeptideCV = false;
            UpdateRetentionTimeGraph();
        }

        private void timeRTValueContextMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.RTPeptideValue = RTPeptideValue.Retention.ToString();
            UpdateRetentionTimeGraph();
        }

        private void fwhmRTValueContextMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.RTPeptideValue = RTPeptideValue.FWHM.ToString();
            UpdateRetentionTimeGraph();
        }

        private void fwbRTValueContextMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.RTPeptideValue = RTPeptideValue.FWB.ToString();
            UpdateRetentionTimeGraph();
        }

        private void timePropsContextMenuItem_Click(object sender, EventArgs e)
        {
            ShowRTPropertyDlg();
        }

        public void ShowRTPropertyDlg()
        {
            GraphTypeRT graphType = RTGraphController.GraphType;
            if (graphType == GraphTypeRT.schedule)
            {
                using (var dlg = new SchedulingGraphPropertyDlg())
                {
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                    {
                        UpdateRetentionTimeGraph();
                    }
                }
            }
            else
            {
                using (var dlg = new RTChartPropertyDlg())
                {
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                    {
                        UpdateSummaryGraphs();
                    }
                }
            }
        }

        private void UpdateRetentionTimeGraph()
        {
            if (_graphRetentionTime != null)
            {
                try
                {
                    _graphRetentionTime.UpdateUI();
                }
                catch (CalculatorException e)
                {
                    MessageDlg.Show(this, e.Message);
                    Settings.Default.RTCalculatorName = "";
                }
            }
        }

        #endregion

        #region Peak area graph

        public GraphSummary GraphPeakArea { get { return _graphPeakArea; } }

        public void ShowGraphPeakArea(bool show)
        {
            if (show)
            {
                if (_graphPeakArea != null)
                {
                    _graphPeakArea.Activate();
                }
                else
                {
                    _graphPeakArea = CreateGraphPeakArea();

                    // Choose a position to float the window
                    var rectFloat = dockPanel.Bounds;
                    rectFloat = dockPanel.RectangleToScreen(rectFloat);
                    rectFloat.X += rectFloat.Width / 4;
                    rectFloat.Y += rectFloat.Height / 3;
                    rectFloat.Width = Math.Max(600, rectFloat.Width / 2);
                    rectFloat.Height = Math.Max(440, rectFloat.Height / 2);
                    // Make sure it is on the screen.
                    var screen = Screen.FromControl(dockPanel);
                    var rectScreen = screen.WorkingArea;
                    rectFloat.X = Math.Max(rectScreen.X, Math.Min(rectScreen.Width - rectFloat.Width, rectFloat.X));
                    rectFloat.Y = Math.Max(rectScreen.Y, Math.Min(rectScreen.Height - rectFloat.Height, rectFloat.Y));

                    _graphPeakArea.Show(dockPanel, rectFloat);
                }
            }
            else if (_graphPeakArea != null)
            {
                // Save current setting for showing spectra
                show = Settings.Default.ShowPeakAreaGraph;
                // Close the spectrum graph window
                _graphPeakArea.Hide();
                // Restore setting and menuitem from saved value
                Settings.Default.ShowPeakAreaGraph = show;
            }
        }

        private GraphSummary CreateGraphPeakArea()
        {
            _graphPeakArea = new GraphSummary(this, new AreaGraphController()) {TabText = "Peak Areas"};
            _graphPeakArea.FormClosed += graphPeakArea_FormClosed;
            _graphPeakArea.VisibleChanged += graphPeakArea_VisibleChanged;
            return _graphPeakArea;
        }

        private void DestroyGraphPeakArea()
        {
            if (_graphPeakArea != null)
            {
                _graphPeakArea.FormClosed -= graphPeakArea_FormClosed;
                _graphPeakArea.VisibleChanged -= graphPeakArea_VisibleChanged;
                _graphPeakArea.Close();
                _graphPeakArea = null;
            }
        }

        private void graphPeakArea_VisibleChanged(object sender, EventArgs e)
        {
            Settings.Default.ShowPeakAreaGraph = (_graphPeakArea != null && _graphPeakArea.Visible);
        }

        private void graphPeakArea_FormClosed(object sender, FormClosedEventArgs e)
        {
            // Update settings and menu check
            Settings.Default.ShowPeakAreaGraph = false;

            _graphPeakArea = null;
        }

        private void BuildAreaGraphMenu(ToolStrip menuStrip)
        {
            // Store original menuitems in an array, and insert a separator
            ToolStripItem[] items = new ToolStripItem[menuStrip.Items.Count];
            int iUnzoom = -1;
            for (int i = 0; i < items.Length; i++)
            {
                items[i] = menuStrip.Items[i];
                string tag = (string)items[i].Tag;
                if (tag == "unzoom")
                    iUnzoom = i;
            }

            if (iUnzoom != -1)
                menuStrip.Items.Insert(iUnzoom, toolStripSeparator25); // TODO: Use another separator?

            // Insert skyline specific menus
            var set = Settings.Default;
            int iInsert = 0;
            menuStrip.Items.Insert(iInsert++, areaGraphContextMenuItem);
            if (areaGraphContextMenuItem.DropDownItems.Count == 0)
            {
                areaGraphContextMenuItem.DropDownItems.AddRange(new ToolStripItem[]
                {
                    areaReplicateComparisonContextMenuItem,
                    areaPeptideComparisonContextMenuItem
                });
            }

            GraphTypeArea graphType = AreaGraphController.GraphType;
            menuStrip.Items.Insert(iInsert++, toolStripSeparator16);
            menuStrip.Items.Insert(iInsert++, transitionsContextMenuItem);
            // Sometimes child menuitems are stripped from the parent
            if (transitionsContextMenuItem.DropDownItems.Count == 0)
            {
                transitionsContextMenuItem.DropDownItems.AddRange(new ToolStripItem[]
                {
                    allTranContextMenuItem,
                    precursorsTranContextMenuItem,
                    productsTranContextMenuItem,
                    singleTranContextMenuItem,
                    totalTranContextMenuItem
                });
            }

            if (graphType == GraphTypeArea.replicate)
            {
                menuStrip.Items.Insert(iInsert++, replicateOrderContextMenuItem);
                if (replicateOrderContextMenuItem.DropDownItems.Count == 0)
                {
                    replicateOrderContextMenuItem.DropDownItems.AddRange(new ToolStripItem[]
                        {
                            replicateOrderDocumentContextMenuItem,
                            replicateOrderAcqTimeContextMenuItem
                        });
                }
                areaNormalizeTotalContextMenuItem.Checked = 
                    (AreaGraphController.AreaView == AreaNormalizeToView.area_percent_view);
                menuStrip.Items.Insert(iInsert++, areaNormalizeContextMenuItem);
                if (areaNormalizeContextMenuItem.DropDownItems.Count == 0)
                {
                    areaNormalizeContextMenuItem.DropDownItems.AddRange(new[]
                        {
                            areaNormalizeMaximumContextMenuItem,
                            areaNormalizeTotalContextMenuItem,
                            (ToolStripItem)toolStripSeparator40,
                            areaNormalizeNoneContextMenuItem
                        });                 
                }
                var areaReplicateGraphPane = _graphPeakArea.GraphPane as AreaReplicateGraphPane;
                if (areaReplicateGraphPane != null)
                {
                    // If the area replicate graph is being displayed and it shows a legend, 
                    // display the "Legend" option
                    if (areaReplicateGraphPane.CanShowPeakAreaLegend)
                    {
                        showPeakAreaLegendContextMenuItem.Checked = set.ShowPeakAreaLegend;
                        menuStrip.Items.Insert(iInsert++, showPeakAreaLegendContextMenuItem);
                    }

                    // If the area replicate graph is being displayed and it can show a library,
                    // display the "Show Library" option
                    var expectedVisible = areaReplicateGraphPane.ExpectedVisible;
                    if (expectedVisible != AreaExpectedValue.none)
                    {
                        showLibraryPeakAreaContextMenuItem.Checked = set.ShowLibraryPeakArea;
                        showLibraryPeakAreaContextMenuItem.Text = expectedVisible == AreaExpectedValue.library
                                                                      ? "Show Library"
                                                                      : "Show Expected";
                        menuStrip.Items.Insert(iInsert++, showLibraryPeakAreaContextMenuItem);
                    }

                    // If the area replicate graph is being displayed and it can show dot products,
                    // display the "Show Dot Product" option
                    if (areaReplicateGraphPane.CanShowDotProduct)
                    {
                        showDotProductToolStripMenuItem.Checked = set.ShowDotProductPeakArea;
                        menuStrip.Items.Insert(iInsert++, showDotProductToolStripMenuItem);
                    }
                } 
            }
            else if (graphType == GraphTypeArea.peptide)
            {
                menuStrip.Items.Insert(iInsert++, peptideOrderContextMenuItem);
                if (peptideOrderContextMenuItem.DropDownItems.Count == 0)
                {
                    peptideOrderContextMenuItem.DropDownItems.AddRange(new ToolStripItem[]
                        {
                            peptideOrderDocumentContextMenuItem,
                            peptideOrderRTContextMenuItem,
                            peptideOrderAreaContextMenuItem
                        });
                }

                if (DocumentUI.Settings.HasResults &&
                    DocumentUI.Settings.MeasuredResults.Chromatograms.Count > 1)
                {
                    menuStrip.Items.Insert(iInsert++, replicatesRTContextMenuItem);
                    if (replicatesRTContextMenuItem.DropDownItems.Count == 0)
                    {
                        replicatesRTContextMenuItem.DropDownItems.AddRange(new ToolStripItem[]
                        {
                            averageReplicatesContextMenuItem,
                            singleReplicateRTContextMenuItem,
                            bestReplicateRTContextMenuItem
                        });
                    }
                }
                menuStrip.Items.Insert(iInsert++, scopeContextMenuItem);
                if (scopeContextMenuItem.DropDownItems.Count == 0)
                {
                    scopeContextMenuItem.DropDownItems.AddRange(new ToolStripItem[]
                        {
                            documentScopeContextMenuItem,
                            proteinScopeContextMenuItem
                        });
                }
                menuStrip.Items.Insert(iInsert++, peptideCvsContextMenuItem);
                peptideCvsContextMenuItem.Checked = set.ShowPeptideCV;
            }
            menuStrip.Items.Insert(iInsert++, peptideLogScaleContextMenuItem);
            peptideLogScaleContextMenuItem.Checked = set.AreaLogScale;
            selectionContextMenuItem.Checked = set.ShowReplicateSelection;
            menuStrip.Items.Insert(iInsert++, selectionContextMenuItem);

            menuStrip.Items.Insert(iInsert++, toolStripSeparator24);
            menuStrip.Items.Insert(iInsert++, areaPropsContextMenuItem);
            menuStrip.Items.Insert(iInsert, toolStripSeparator28);

            // Remove some ZedGraph menu items not of interest
            foreach (var item in items)
            {
                string tag = (string)item.Tag;
                if (tag == "set_default" || tag == "show_val")
                    menuStrip.Items.Remove(item);
            }
        }

        private void areaGraphMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            GraphTypeArea graphType = AreaGraphController.GraphType;
            areaReplicateComparisonMenuItem.Checked = areaReplicateComparisonContextMenuItem.Checked = (graphType == GraphTypeArea.replicate);
            areaPeptideComparisonMenuItem.Checked = areaPeptideComparisonContextMenuItem.Checked = (graphType == GraphTypeArea.peptide);
        }

        private void areaReplicateComparisonMenuItem_Click(object sender, EventArgs e)
        {
            ShowPeakAreaReplicateComparison();
        }

        public void ShowPeakAreaReplicateComparison()
        {
            Settings.Default.AreaGraphType = GraphTypeArea.replicate.ToString();
            ShowGraphPeakArea(true);
            UpdatePeakAreaGraph();
        }

        private void areaPeptideComparisonMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.AreaGraphType = GraphTypeArea.peptide.ToString();
            ShowGraphPeakArea(true);
            UpdatePeakAreaGraph();
        }

        private void replicateOrderContextMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            SummaryReplicateOrder replicateOrder = SummaryReplicateGraphPane.ReplicateOrder;
            replicateOrderDocumentContextMenuItem.Checked = (replicateOrder == SummaryReplicateOrder.document);
            replicateOrderAcqTimeContextMenuItem.Checked = (replicateOrder == SummaryReplicateOrder.time);
        }

        private void replicateOrderDocumentContextMenuItem_Click(object sender, EventArgs e)
        {
            ShowReplicateOrder(SummaryReplicateOrder.document);
        }

        private void replicateOrderAcqTimeContextMenuItem_Click(object sender, EventArgs e)
        {
            ShowReplicateOrder(SummaryReplicateOrder.time);
        }

        public void ShowReplicateOrder(SummaryReplicateOrder order)
        {
            SummaryReplicateGraphPane.ReplicateOrder = order;
            UpdateSummaryGraphs();
        }

        private void scopeContextMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            var areaScope = AreaGraphController.AreaScope;
            documentScopeContextMenuItem.Checked = (areaScope == AreaScope.document);
            proteinScopeContextMenuItem.Checked = (areaScope == AreaScope.protein);
        }

        private void documentScopeContextMenuItem_Click(object sender, EventArgs e)
        {
            AreaScopeTo(AreaScope.document);
        }

        private void proteinScopeContextMenuItem_Click(object sender, EventArgs e)
        {
            AreaScopeTo(AreaScope.protein);
        }

        public void AreaScopeTo(AreaScope areaScope)
        {
            AreaGraphController.AreaScope = areaScope;
            UpdateSummaryGraphs();
        }

        private void peptideOrderContextMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            SummaryPeptideOrder peptideOrder = SummaryPeptideGraphPane.PeptideOrder;
            peptideOrderDocumentContextMenuItem.Checked = (peptideOrder == SummaryPeptideOrder.document);
            peptideOrderRTContextMenuItem.Checked = (peptideOrder == SummaryPeptideOrder.time);
            peptideOrderAreaContextMenuItem.Checked = (peptideOrder == SummaryPeptideOrder.area);
        }

        private void peptideOrderDocumentContextMenuItem_Click(object sender, EventArgs e)
        {
            ShowPeptideOrder(SummaryPeptideOrder.document);
        }

        private void peptideOrderRTContextMenuItem_Click(object sender, EventArgs e)
        {
            ShowPeptideOrder(SummaryPeptideOrder.time);
        }

        private void peptideOrderAreaContextMenuItem_Click(object sender, EventArgs e)
        {
            ShowPeptideOrder(SummaryPeptideOrder.area);
        }

        public void ShowPeptideOrder(SummaryPeptideOrder order)
        {
            SummaryPeptideGraphPane.PeptideOrder = order;
            UpdateSummaryGraphs();
        }

        public void NormalizeAreaGraphTo(AreaNormalizeToView areaView)
        {
            AreaGraphController.AreaView = areaView;
            if (AreaGraphController.AreaView == AreaNormalizeToView.area_percent_view ||
                AreaGraphController.AreaView == AreaNormalizeToView.area_maximum_view)
                Settings.Default.AreaLogScale = false;
            UpdatePeakAreaGraph();
        }

        private void peptideLogScaleContextMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.AreaLogScale = peptideLogScaleContextMenuItem.Checked;
            if (peptideLogScaleContextMenuItem.Checked)
                AreaGraphController.AreaView = AreaNormalizeToView.none;
            UpdateSummaryGraphs();
        }

        private void peptideCvsContextMenuItem_Click(object sender, EventArgs e)
        {
            ShowCVValues();
        }

        public void ShowCVValues()
        {
            Settings.Default.ShowPeptideCV = peptideCvsContextMenuItem.Checked;
            // Showing CVs only makes sense for Replicates = All
            Settings.Default.ShowRegressionReplicateEnum = ReplicateDisplay.all.ToString();
            // Showing CVs does not make sense for All retention time values at once
            if (RTPeptideGraphPane.RTValue == RTPeptideValue.All)
                Settings.Default.RTPeptideValue = RTPeptideValue.Retention.ToString();
            UpdateSummaryGraphs();
        }

        private void areaPropsContextMenuItem_Click(object sender, EventArgs e)
        {
            using (var dlg = new AreaChartPropertyDlg())
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    UpdateSummaryGraphs();
                }
            }
        }

        private void areaNormalizeContextMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            ToolStripMenuItem menu = areaNormalizeContextMenuItem;
            // Remove menu items up to the "Maximum" menu item.
            while (!ReferenceEquals(areaNormalizeMaximumContextMenuItem, menu.DropDownItems[0]))
                menu.DropDownItems.RemoveAt(0);
            
            var areaView = AreaGraphController.AreaView;
            var standardTypes = DocumentUI.Settings.PeptideSettings.Modifications.InternalStandardTypes;

            // Add the Heavy option to the areaNormalizeContextMenuItem if there are heavy modifications
            if (DocumentUI.Settings.PeptideSettings.Modifications.HasHeavyModifications)
            {
                for (int i = 0; i < standardTypes.Count; i++)
                {
                    var handler = new SelectNormalizeHandler(this, i);
                    var item = new ToolStripMenuItem(standardTypes[i].Title, null, handler.ToolStripMenuItemClick)
                                   {
                                       Checked =
                                           (SequenceTree.RatioIndex == i &&
                                            areaView == AreaNormalizeToView.area_ratio_view)
                                   };
                    menu.DropDownItems.Insert(i, item);
                }
            }

            areaNormalizeTotalContextMenuItem.Checked = (areaView == AreaNormalizeToView.area_percent_view);
            areaNormalizeMaximumContextMenuItem.Checked = (areaView == AreaNormalizeToView.area_maximum_view);
            areaNormalizeNoneContextMenuItem.Checked = (areaView == AreaNormalizeToView.none);
        }

        private class SelectNormalizeHandler : SelectRatioHandler
        {
            public SelectNormalizeHandler(SkylineWindow skyline, int ratioIndex) : base(skyline, ratioIndex)
            {
            }

            protected override void OnMenuItemClick()
            {
                AreaGraphController.AreaView = AreaNormalizeToView.area_ratio_view;

                base.OnMenuItemClick();

                _skyline.UpdatePeakAreaGraph();
            }
        }

        private void areaNormalizeTotalContextMenuItem_Click(object sender, EventArgs e)
        {
            NormalizeAreaGraphTo(AreaNormalizeToView.area_percent_view);
        }

        private void areaNormalizeNoneContextMenuItem_Click(object sender, EventArgs e)
        {
            NormalizeAreaGraphTo(AreaNormalizeToView.none);
        }

        private void areaNormalizeMaximumContextMenuItem_Click(object sender, EventArgs e)
        {
            NormalizeAreaGraphTo(AreaNormalizeToView.area_maximum_view);
        }

        private void showLibraryPeakAreaContextMenuItem_Click(object sender, EventArgs e)
        {
            // Show/hide the library column in the peak area view.
            Settings.Default.ShowLibraryPeakArea = !Settings.Default.ShowLibraryPeakArea;
            UpdateSummaryGraphs();
        }

        private void showDotProductToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.ShowDotProductPeakArea = !Settings.Default.ShowDotProductPeakArea;
            UpdateSummaryGraphs();
        }

        private void showPeakAreaLegendContextMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.ShowPeakAreaLegend = !Settings.Default.ShowPeakAreaLegend;
            UpdateSummaryGraphs();
        }

        public void UpdatePeakAreaGraph()
        {
            if (_graphPeakArea != null)
                _graphPeakArea.UpdateUI();
        }

        private void UpdateSummaryGraphs()
        {
            UpdateRetentionTimeGraph();
            UpdatePeakAreaGraph();            
        }

        #endregion

        #region Results Grid

        private void resultsGridMenuItem_Click(object sender, EventArgs e)
        {
            ShowResultsGrid(Settings.Default.ShowResultsGrid = true);
        }

        public void ShowResultsGrid(bool show)
        {
            if (show)
            {
                if (_resultsGridForm != null)
                {
                    _resultsGridForm.Activate();
                }
                else
                {
                    _resultsGridForm = CreateResultsGrid();

                    // TODO(nicksh): this code was copied from ShowGraphRetentionTime
                    // coalesce duplicate code
                    var rectFloat = dockPanel.Bounds;
                    rectFloat = dockPanel.RectangleToScreen(rectFloat);
                    rectFloat.X += rectFloat.Width / 4;
                    rectFloat.Y += rectFloat.Height / 3;
                    rectFloat.Width = Math.Max(600, rectFloat.Width / 2);
                    rectFloat.Height = Math.Max(440, rectFloat.Height / 2);
                    // Make sure it is on the screen.
                    var screen = Screen.FromControl(dockPanel);
                    var rectScreen = screen.WorkingArea;
                    rectFloat.X = Math.Max(rectScreen.X, Math.Min(rectScreen.Width - rectFloat.Width, rectFloat.X));
                    rectFloat.Y = Math.Max(rectScreen.Y, Math.Min(rectScreen.Height - rectFloat.Height, rectFloat.Y));

                    _resultsGridForm.Show(dockPanel, rectFloat);
                }
            }
            else
            {
                if (_resultsGridForm != null)
                {
                    _resultsGridForm.Hide();
                }
            }
        }

        private ResultsGridForm CreateResultsGrid()
        {
            _resultsGridForm = new ResultsGridForm(this, SequenceTree);
            _resultsGridForm.FormClosed += resultsGrid_FormClosed;
            _resultsGridForm.VisibleChanged += resultsGrid_VisibleChanged;
            return _resultsGridForm;
        }

        private void DestroyResultsGrid()
        {
            if (_resultsGridForm != null)
            {
                _resultsGridForm.FormClosed -= resultsGrid_FormClosed;
                _resultsGridForm.VisibleChanged -= resultsGrid_VisibleChanged;
                _resultsGridForm.Close();
                _resultsGridForm = null;
            }
        }

        private void resultsGrid_VisibleChanged(object sender, EventArgs e)
        {
            Settings.Default.ShowResultsGrid = (_resultsGridForm != null && _resultsGridForm.Visible);
        }

        void resultsGrid_FormClosed(object sender, FormClosedEventArgs e)
        {
            // Update settings and menu check
            Settings.Default.ShowResultsGrid = false;

            _resultsGridForm = null;
        }

        #endregion

        #region Graph layout

        private const double MAX_TILED_ASPECT_RATIO = 2;

        private void arrangeTiledMenuItem_Click(object sender, EventArgs e)
        {
            ArrangeGraphsTiled();
        }

        public void ArrangeGraphsTiled()
        {
            var listGraphs = GetArrangeableGraphs();
            if (listGraphs.Count < 2)
                return;
            using (new DockPanelLayoutLock(dockPanel, true))
            {
                ArrangeGraphsGrouped(listGraphs, listGraphs.Count, GroupGraphsType.separated);
            }
        }

        private void arrangeTabbedMenuItem_Click(object sender, EventArgs e)
        {
            ArrangeGraphsTabbed();
        }

        public void ArrangeGraphsTabbed()
        {
            var listGraphs = GetArrangeableGraphs();
            if (listGraphs.Count < 2)
                return;
            using (new DockPanelLayoutLock(dockPanel, true))
            {
                ArrangeGraphsTabbed(listGraphs);
            }
        }

        private void arrangeGroupedMenuItem_Click(object sender, EventArgs e)
        {
            ArrangeGraphsGrouped();
        }

        public void ArrangeGraphsGrouped()
        {
            var order = Helpers.ParseEnum(Settings.Default.ArrangeGraphsOrder, GroupGraphsOrder.Position);
            bool reversed = Settings.Default.ArrangeGraphsReversed;
            var listGraphs = GetArrangeableGraphs(order, reversed);

            using (var dlg = new ArrangeGraphsGroupedDlg(listGraphs.Count))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    if (order != dlg.GroupOrder || reversed != dlg.Reversed)
                        listGraphs = GetArrangeableGraphs(dlg.GroupOrder, dlg.Reversed);
                    if (listGraphs.Count < 2)
                        return;

                    using (new DockPanelLayoutLock(dockPanel, true))
                    {
                        ArrangeGraphsGrouped(listGraphs, dlg.Groups, dlg.GroupType);
                    }
                }
            }
        }

        private void ArrangeGraphsGrouped(IList<DockableForm> listGraphs, int groups, GroupGraphsType groupType)
        {
            // First just arrange everything into a single pane
            ArrangeGraphsTabbed(listGraphs);

            // Figure out how to distribute the panes into rows and columns
            var documentPane = FindPane(listGraphs[0]);
            double width = documentPane.Width;
            double height = documentPane.Height;
            int rows = 1;
            while ((height/rows) / (width/(groups/rows + (groups % rows > 0 ? 1 : 0))) > MAX_TILED_ASPECT_RATIO)
                rows++;

            int longRows = groups%rows;
            int columnsShort = groups/rows;

            // Distribute the forms into lists representing rows, columns, and groups
            var listTiles = new List<List<List<DockableForm>>>();
            if (groupType == GroupGraphsType.distributed)
            {
                // As if dealing a card deck over the groups
                int iForm = 0;
                int forms = listGraphs.Count;
                while (iForm < listGraphs.Count)
                {
                    for (int iRow = 0; iRow < rows; iRow++)
                    {
                        if (listTiles.Count <= iRow)
                            listTiles.Add(new List<List<DockableForm>>());
                        var rowTiles = listTiles[iRow];
                        int columns = columnsShort + (iRow < longRows ? 1 : 0);
                        for (int iCol = 0; iCol < columns && iForm < forms; iCol++)
                        {
                            if (rowTiles.Count <= iCol)
                                rowTiles.Add(new List<DockableForm>());
                            var tabbedForms = rowTiles[iCol];
                            tabbedForms.Add(listGraphs[iForm++]);
                        }
                    }
                }
            }
            else
            {
                // Filling each group before continuing to the next
                int count = listGraphs.Count;
                int longGroups = count % groups;
                int tabsShort = count / groups;
                for (int iRow = 0, iGroup = 0, iForm = 0; iRow < rows; iRow++)
                {
                    var rowTiles = new List<List<DockableForm>>();
                    listTiles.Add(rowTiles);
                    int columns = columnsShort + (iRow < longRows ? 1 : 0);
                    for (int iCol = 0; iCol < columns; iCol++)
                    {
                        var tabbedForms = new List<DockableForm>();
                        rowTiles.Add(tabbedForms);
                        int tabs = tabsShort + (iGroup++ < longGroups ? 1 : 0);
                        for (int iTab = 0; iTab < tabs; iTab++)
                        {
                            tabbedForms.Add(listGraphs[iForm++]);                            
                        }
                    }
                }                
            }

            // Place the forms in the dock panel
            // Rows first
            for (int i = 1; i < rows; i++)
            {
                PlacePane(i, 0, rows, DockPaneAlignment.Bottom, listTiles);
            }
            // Then columns in the rows
            for (int i = 0; i < rows; i++)
            {
                int columns = listTiles[i].Count;
                for (int j = 1; j < columns; j++)
                {
                    PlacePane(i, j, columns, DockPaneAlignment.Right, listTiles);
                }
            }            
        }

        private void PlacePane(int row, int col, int count,
            DockPaneAlignment alignment, IList<List<List<DockableForm>>> listTiles)
        {
            DockableForm previousForm = alignment == DockPaneAlignment.Bottom
                                            ? listTiles[row - 1][col][0]
                                            : listTiles[row][col - 1][0];
            DockPane previousPane = FindPane(previousForm);
            var groupForms = listTiles[row][col];
            var dockableForm = groupForms[0];
            int dim = alignment == DockPaneAlignment.Bottom ? row : col;
            dockableForm.Show(previousPane, alignment,
                              ((double)(count - dim)) / (count - dim + 1));
            ArrangeGraphsTabbed(groupForms);
        }

        private void ArrangeGraphsTabbed(IList<DockableForm> groupForms)
        {
            if (groupForms.Count < 2)
                return;
            DockPane primaryPane = FindPane(groupForms[0]);
            for (int i = 1; i < groupForms.Count; i++)
                groupForms[i].Show(primaryPane, null);
        }

        private List<DockableForm> GetArrangeableGraphs()
        {
            var order = Helpers.ParseEnum(Settings.Default.ArrangeGraphsOrder, GroupGraphsOrder.Position);
            return GetArrangeableGraphs(order, Settings.Default.ArrangeGraphsReversed);
        }

        private List<DockableForm> GetArrangeableGraphs(GroupGraphsOrder order, bool reversed)
        {
            IList<DockPane> listPanes = dockPanel.Panes;
            if (order == GroupGraphsOrder.Position)
            {
                var listPanesSorted = new List<DockPane>();
                foreach (var pane in dockPanel.Panes)
                {
                    if (pane.IsHidden || pane.DockState != DockState.Document)
                        continue;
                    listPanesSorted.Add(pane);
                }
                listPanesSorted.Sort((p1, p2) =>
                {
                    if (p1.Top != p2.Top)
                        return p1.Top - p2.Top;
                    return p1.Left - p2.Left;
                });
                if (reversed)
                    listPanesSorted.Reverse();
                listPanes = listPanesSorted;
            }

            var listGraphs = new List<DockableForm>();
            foreach (var pane in listPanes)
            {
                IEnumerable<IDockableForm> listForms = pane.Contents;
                if (order == GroupGraphsOrder.Position && reversed)
                    listForms = listForms.Reverse();
                foreach (DockableForm dockableForm in listForms)
                {
                    if (dockableForm.IsHidden || dockableForm.DockState != DockState.Document)
                        continue;
                    listGraphs.Add(dockableForm);
                }                
            }

            if (order == GroupGraphsOrder.Document)
            {
                // Populate a dictionary with the desired document order
                var dictOrder = new Dictionary<DockableForm, int>();
                int iOrder = 0;
                if (_graphSpectrum != null)
                    dictOrder.Add(_graphSpectrum, iOrder++);
                if (_graphRetentionTime != null)
                    dictOrder.Add(_graphRetentionTime, iOrder++);
                if (_graphPeakArea != null)
                    dictOrder.Add(_graphPeakArea, iOrder++);
                if (DocumentUI.Settings.HasResults)
                {
                    foreach (var chromatograms in DocumentUI.Settings.MeasuredResults.Chromatograms)
                    {
                        var graphChrom = GetGraphChrom(chromatograms.Name);
                        if (graphChrom != null)
                            dictOrder.Add(graphChrom, iOrder++);
                    }
                }
                // Make sure everything is represented, though it should
                // already be.
                foreach (var graph in listGraphs)
                {
                    int i;
                    if (!dictOrder.TryGetValue(graph, out i))
                        dictOrder.Add(graph, iOrder++);
                }

                // Sort the list of visible document panes by the document order
                // in the dictionary
                listGraphs.Sort((g1, g2) => dictOrder[g1] - dictOrder[g2]);
                if (reversed)
                    listGraphs.Reverse();
            }
            return listGraphs;
        }

        #endregion
    }
}
