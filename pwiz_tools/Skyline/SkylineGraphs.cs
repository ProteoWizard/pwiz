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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using DigitalRune.Windows.Docking;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.AuditLog;
using pwiz.Skyline.Controls.Graphs.Calibration;
using pwiz.Skyline.Controls.GroupComparison;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.ElementLocators.ExportAnnotations;
using pwiz.Skyline.Model.RetentionTimes;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using ZedGraph;
using pwiz.Skyline.Util.Extensions;
using PeptideDocNode = pwiz.Skyline.Model.PeptideDocNode;
using Transition = pwiz.Skyline.Model.Transition;

namespace pwiz.Skyline
{
    public partial class SkylineWindow :
        GraphSpectrum.IStateProvider,
        GraphChromatogram.IStateProvider,
        GraphSummary.IStateProvider
    {
        private GraphSpectrum _graphSpectrum;
        private GraphFullScan _graphFullScan;
        private readonly GraphSpectrumSettings _graphSpectrumSettings;

        private readonly List<GraphSummary> _listGraphRetentionTime = new List<GraphSummary>();
        private readonly List<GraphSummary> _listGraphPeakArea = new List<GraphSummary>();
        private readonly List<GraphSummary> _listGraphMassError = new List<GraphSummary>();

        private DockableForm _resultsGridForm;
        private DocumentGridForm _documentGridForm;
        private CalibrationForm _calibrationForm;
        private AuditLogForm _auditLogForm;
        public static int MAX_GRAPH_CHROM = 100; // Never show more than this many chromatograms, lest we hit the Windows handle limit
        private readonly List<GraphChromatogram> _listGraphChrom = new List<GraphChromatogram>(); // List order is MRU, with oldest in position 0
        private bool _inGraphUpdate;
        private ChromFileInfoId _alignToFile;
        private bool _alignToPrediction;

        public RTGraphController RTGraphController
        {
            get
            {
                var active = _listGraphRetentionTime.FirstOrDefault();

                if (active == null)
                    return null;
                return active.Controller as RTGraphController;
            }
        }

        private GraphSummary ContextMenuGraphSummary { get; set; }

        private void dockPanel_ActiveDocumentChanged(object sender, EventArgs e)
        {
            try
            {
                ActiveDocumentChanged();
            }
            catch (Exception x)
            {
                Program.ReportException(x);
            }
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
            _listGraphPeakArea.ForEach(g => g.ActiveLibrary = activeLibrary);
            _listGraphRetentionTime.ForEach(g => g.ActiveLibrary = activeLibrary);

            foreach (var graphChrom in _listGraphChrom.ToArray()) // List may be updating concurrent with this access, so convert to array first
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
            private HashSet<Control> _suspendedControls;

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
                    Assume.IsNull(_suspendedControls);
                    _suspendedControls = new HashSet<Control>();
                    foreach (var pane in _dockPanel.Panes)
                    {
                        EnsurePaneLocked(pane);
                    }
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
                if (_suspendedControls != null)
                {
                    foreach (var control in _suspendedControls)
                    {
                        control.ResumeLayout();
                    }
                    _suspendedControls = null;
                }
                _dockPanel = null;  // Only once
            }

            /// <summary>
            /// Ensures that "SuspendControl" has been called on the DockPane, as well
            /// as its controls (specifically its DockPaneStrip which spends a long time
            /// redrawing as each child is added).
            /// </summary>
            /// <param name="dockPane"></param>
            public void EnsurePaneLocked(DockPane dockPane)
            {
                if (SuspendControl(dockPane))
                {
                    foreach (var control in dockPane.Controls.OfType<Control>())
                    {
                        SuspendControl(control);
                    }
                }
            }

            /// <summary>
            /// Ensures SuspendLayout has called on the control
            /// </summary>
            /// <returns>false if the control has already been suspended</returns>
            private bool SuspendControl(Control control)
            {
                if (control == null || _suspendedControls == null)
                {
                    return false;
                }
                if (!_suspendedControls.Add(control))
                {
                    return false;
                }
                control.SuspendLayout();
                return true;
            }
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
                    if (_listGraphMassError.Any() || _listGraphPeakArea.Any() || _listGraphRetentionTime.Any())
                        UpdateGraphPanes(new List<IUpdatable>(_listGraphMassError.Concat(_listGraphPeakArea.Concat(_listGraphRetentionTime))));
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
                if (!ArrayUtil.EqualsDeep(filterNew.PeptideIonTypes, filterOld.PeptideIonTypes) ||
                    !ArrayUtil.EqualsDeep(filterNew.SmallMoleculeIonTypes, filterOld.SmallMoleculeIonTypes))
                {
                    // Only turn off old ion types, if new settings are not MS1-only full-scan
                    var fullScan = settingsNew.TransitionSettings.FullScan;
                    var enablePeptides = DocumentUI.DocumentType != SrmDocument.DOCUMENT_TYPE.small_molecules;
                    var enableSmallMolecules = DocumentUI.HasSmallMolecules;
                    if (!fullScan.IsEnabled || fullScan.IsEnabledMsMs)
                    {
                        CheckIonTypes(filterOld.PeptideIonTypes, false, enablePeptides);
                        CheckIonTypes(filterOld.SmallMoleculeIonTypes, false, enableSmallMolecules);
                    }
                    CheckIonTypes(filterNew.PeptideIonTypes, true, enablePeptides);
                    CheckIonTypes(filterNew.SmallMoleculeIonTypes, true, enableSmallMolecules);
                    refresh = true;
                }

                // Charge selection
                if (!ArrayUtil.EqualsDeep(filterNew.PeptideProductCharges, filterOld.PeptideProductCharges) ||
                    !ArrayUtil.EqualsDeep(filterNew.SmallMoleculeFragmentAdducts, filterOld.SmallMoleculeFragmentAdducts))
                {
                    // First clear any old charge enabling
                    CheckIonCharges(filterOld.PeptideProductCharges, false);
                    CheckIonCharges(filterOld.SmallMoleculeFragmentAdducts, false);
                    // Then enable based on settings and document contents
                    switch (DocumentUI.DocumentType)
                    {
                        case SrmDocument.DOCUMENT_TYPE.none:
                        case SrmDocument.DOCUMENT_TYPE.proteomic:
                            CheckIonCharges(filterNew.PeptideProductCharges, true);
                            break;
                        case SrmDocument.DOCUMENT_TYPE.small_molecules:
                            CheckIonCharges(filterNew.SmallMoleculeFragmentAdducts, true);
                            break;
                        case SrmDocument.DOCUMENT_TYPE.mixed:
                            CheckIonCharges(filterNew.PeptideProductCharges, true);
                            CheckIonCharges(filterNew.PeptideProductCharges, true);
                            break;
                    }
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
                        var message = TextUtil.LineSeparate(string.Format(Resources.SkylineWindow_UpdateGraphUI_Failure_attempting_to_load_the_window_layout_file__0__, layoutFile),
                                                                            Resources.SkylineWindow_UpdateGraphUI_Rename_or_delete_this_file_to_restore_the_default_layout, 
                                                                            Resources.SkylineWindow_UpdateGraphUI_Skyline_may_also_need_to_be_restarted);
                        throw new IOException(message, x);
                    }
                }

                UpdateIonTypesMenuItemsVisibility();
                if (!graphsToolStripMenuItem.Enabled)
                {
                    graphsToolStripMenuItem.Enabled = true;
                    ionTypesMenuItem.Enabled = true;
                    chargesMenuItem.Enabled = true;
                    ranksMenuItem.Enabled = true;

                    if (!deserialized)
                    {
                        layoutLock.EnsureLocked();
                        ShowGraphSpectrum(Settings.Default.ShowSpectra);
                    }
                }
                var enable = settingsNew.HasResults;
                bool enableSchedule = IsRetentionTimeGraphTypeEnabled(GraphTypeSummary.schedule);
                bool enableRunToRun = IsRetentionTimeGraphTypeEnabled(GraphTypeSummary.run_to_run_regression);
                if (replicateComparisonMenuItem.Enabled != enable ||
                    retentionTimesMenuItem.Enabled != enableSchedule ||
                    runToRunMenuItem.Enabled != enableRunToRun)
                {
                    retentionTimesMenuItem.Enabled = enableSchedule;
                    replicateComparisonMenuItem.Enabled = enable;
                    timePeptideComparisonMenuItem.Enabled = enable;
                    regressionMenuItem.Enabled = enable;
                    scoreToRunMenuItem.Enabled = enable;
                    runToRunMenuItem.Enabled = runToRunToolStripMenuItem.Enabled = enableRunToRun;
                    schedulingMenuItem.Enabled = enableSchedule;
                    if (!deserialized)
                    {
                        layoutLock.EnsureLocked();
                        UpdateUIGraphRetentionTime(IsRetentionTimeGraphTypeEnabled);
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
                    areaCVHistogramMenuItem.Enabled = enable;
                    areaCVHistogram2DMenuItem.Enabled = enable;

                    if (!deserialized)
                    {
                        layoutLock.EnsureLocked();
                        UpdateUIGraphPeakArea(enable);
                    }
                }
                if (massErrorsMenuItem.Enabled != enable)
                {
                    massErrorsMenuItem.Enabled = enable;
                    massErrorReplicateComparisonMenuItem.Enabled = enable;
                    massErrorPeptideComparisonMenuItem.Enabled = enable;

                    if (!deserialized)
                    {
                        layoutLock.EnsureLocked();
                        UpdateUIGraphMassError(enable);
                    }
                }
                if (_graphFullScan != null && _graphFullScan.Visible && !enable)
                {
                    layoutLock.EnsureLocked();
                    DestroyGraphFullScan();
                }

                if (!ReferenceEquals(settingsNew.MeasuredResults, settingsOld.MeasuredResults))
                {
                    // First hide all graph windows for results that no longer exist in the document
                    foreach (var graphChromatogram in _listGraphChrom.ToArray())
                    {
                        string name = graphChromatogram.NameSet;
                        // Look for matching chromatogram sets across the documents
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
                                if (graphChrom != null)
                                {
                                    RemoveGraphChromFromList(graphChrom);
                                }
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
                                    if (_listGraphChrom.Count < MAX_GRAPH_CHROM) // Limit window count to conserve win32 handles
                                    {
                                        layoutLock.EnsureLocked();
                                        graphChrom = CreateGraphChrom(name, nameLast, false);
                                        layoutLock.EnsurePaneLocked(graphChrom.Pane);

                                        nameFirst = nameFirst ?? name;
                                        nameLast = name;
                                    }
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
                                          where !graph.IsCurrent(settingsOld, settingsNew)
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
                listUpdateGraphs.AddRange(_listGraphRetentionTime);
                listUpdateGraphs.AddRange(_listGraphPeakArea);
                listUpdateGraphs.AddRange(_listGraphMassError);
            }

            UpdateGraphPanes(listUpdateGraphs);
            FoldChangeForm.CloseInapplicableForms(this);
        }

        private void RemoveGraphChromFromList(GraphChromatogram graphChrom)
        {
            _listGraphChrom.Remove(graphChrom);
            DestroyGraphChrom(graphChrom);
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

            var type = RTGraphController.GraphType;
            _listGraphRetentionTime.ToList().ForEach(DestroyGraphRetentionTime);
            RTGraphController.GraphType = type;
           
            type = AreaGraphController.GraphType;
            _listGraphPeakArea.ToList().ForEach(DestroyGraphPeakArea);
            AreaGraphController.GraphType = type;

            type = MassErrorGraphController.GraphType;
            _listGraphMassError.ToList().ForEach(DestroyGraphMassError);
            MassErrorGraphController.GraphType = type;

            FormUtil.OpenForms.OfType<FoldChangeForm>().ForEach(f => f.Close());

            DestroyResultsGrid();
            DestroyDocumentGrid();
            DestroyAuditLogForm();
            DestroyCalibrationForm();

            DestroyImmediateWindow();
            HideFindResults(true);
            foreach (GraphChromatogram graphChrom in _listGraphChrom)
                DestroyGraphChrom(graphChrom);
            _listGraphChrom.Clear();
            DestroyGraphFullScan();
            dockPanel.LoadFromXml(layoutStream, DeserializeForm);
            // SequenceTree resizes often prior to display, so we must restore its scrolling after
            // all resizing has occured
            if (SequenceTree != null)
                SequenceTree.UpdateTopNode();

            EnsureFloatingWindowsVisible();
        }

        public void DestroyAllChromatogramsGraph()
        {
            // Remove any multi-progress left in the list
            lock (_listProgress)
            {
                int multiIndex = _listProgress.IndexOf(s => s is MultiProgressStatus);
                if (multiIndex != -1)
                    _listProgress.RemoveAt(multiIndex);
            }
            if (ImportingResultsWindow != null)
            {
                ImportingResultsWindow.Close();
                ImportingResultsWindow = null;

                // Reset progress for the current document
                _chromatogramManager.ResetProgress(Document);
            }
        }

        public void InvalidateChromatogramGraphs()
        {
            _listGraphChrom.ForEach(graph => graph.IsCacheInvalidated = true);
        }

        private void EnsureFloatingWindowsVisible()
        {
            if (Program.SkylineOffscreen)
                return;

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
            if (persistentString.StartsWith(typeof(SequenceTreeForm).ToString()))
            {
                return _sequenceTreeForm ?? CreateSequenceTreeForm(persistentString);
            }
            else if (Equals(persistentString, typeof(GraphSpectrum).ToString()))
            {
                return _graphSpectrum ?? CreateGraphSpectrum();                
            }

            var split = persistentString.Split('|');
            var splitLength = split.Length;

            // Backward compatibility
            if (persistentString.EndsWith(@"Skyline.Controls.GraphRetentionTime") ||
                splitLength == 2 && split[0] == typeof(GraphSummary).ToString() &&
                split[1] == typeof(RTGraphController).Name)
            {
                var type = RTGraphController.GraphType;
                return _listGraphRetentionTime.FirstOrDefault(g => g.Type == type) ?? CreateGraphRetentionTime(type);
            }

            // Backward compatibility
            if (persistentString.EndsWith(@"Skyline.Controls.GraphPeakArea") ||
            splitLength == 2 && split[0] == typeof(GraphSummary).ToString() &&
                split[1] == typeof(AreaGraphController).Name)
            {
                var type = AreaGraphController.GraphType;
                return _listGraphPeakArea.FirstOrDefault(g => g.Type == type) ?? CreateGraphPeakArea(type);
            }

            // Backward compatibility
            if (splitLength == 2 && split[0] == typeof(GraphSummary).ToString() &&
                split[1] == typeof(MassErrorGraphController).Name)
            {
                var type = MassErrorGraphController.GraphType;
                return _listGraphMassError.FirstOrDefault(g => g.Type == type) ?? CreateGraphMassError(type);
            }

            if (splitLength == 3 && split[0] == typeof(GraphSummary).ToString())
            {
                var type = Helpers.ParseEnum(split[2], GraphTypeSummary.invalid);

                if (split[1] == typeof(RTGraphController).Name)
                    return _listGraphRetentionTime.FirstOrDefault(g => g.Type == type) ?? CreateGraphRetentionTime(type);
                else if (split[1] == typeof(AreaGraphController).Name)
                    return _listGraphPeakArea.FirstOrDefault(g => g.Type == type) ?? CreateGraphPeakArea(type);
                else if (split[1] == typeof(MassErrorGraphController).Name)
                    return _listGraphMassError.FirstOrDefault(g => g.Type == type) ?? CreateGraphMassError(type);
                else
                    return null;
            }

            if (Equals(persistentString, typeof(ResultsGridForm).ToString()) || Equals(persistentString, typeof (LiveResultsGrid).ToString()))
            {
                return _resultsGridForm ?? CreateResultsGrid();
            }
            if (Equals(persistentString, typeof (DocumentGridForm).ToString()))
            {
                return _documentGridForm ?? CreateDocumentGrid();
            }
            if (Equals(persistentString, typeof (CalibrationForm).ToString()))
            {
                return _calibrationForm ?? CreateCalibrationForm();
            }
            if (Equals(persistentString, typeof(AuditLogForm).ToString()))
            {
                return _auditLogForm ?? CreateAuditLogForm();
            }
            if (Equals(persistentString, typeof(ImmediateWindow).ToString()))
            {
                 return _immediateWindow ?? CreateImmediateWindow();
            }
            if (persistentString.StartsWith(typeof(GraphChromatogram).ToString()))
            {
                if (_listGraphChrom.Count >= MAX_GRAPH_CHROM)
                {
                    return null;
                }
                string name = GraphChromatogram.GetTabText(persistentString);
                var settings = DocumentUI.Settings;
                if (settings.HasResults)
                {
                    bool hasName = settings.MeasuredResults.ContainsChromatogram(name);
                    // For tests with persisted layouts containing the default chromatogram name
                    // check for the default name in the current language
                    if (!hasName && Equals(name, Resources.ResourceManager.GetString(
                        @"ImportResultsDlg_DefaultNewName_Default_Name", CultureInfo.InvariantCulture)))
                    {
                        name = Resources.ImportResultsDlg_DefaultNewName_Default_Name;
                        hasName = settings.MeasuredResults.ContainsChromatogram(name);
                    }
                    if (hasName)
                        return GetGraphChrom(name) ?? CreateGraphChrom(name);
                }
            }
            var foldChangeForm = FoldChangeForm.RestoreFoldChangeForm(this, persistentString);
            if (null != foldChangeForm)
            {
                return foldChangeForm;
            }
            if (Equals(persistentString, typeof(GraphFullScan).ToString()))
            {
                return _graphFullScan ?? CreateGraphFullScan();
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

        public void UpdateGraphPanes()
        {
            // Add only visible graphs to the update list, since each update
            // must pass through the Windows message queue on a WM_TIMER.
            var listVisibleChrom = from graphChrom in _listGraphChrom
                                   where graphChrom.Visible
                                   select graphChrom;

            var listUpdateGraphs = new List<IUpdatable>(listVisibleChrom.ToArray());
            
            if (_graphSpectrum != null && _graphSpectrum.Visible)
                listUpdateGraphs.Add(_graphSpectrum);
            listUpdateGraphs.AddRange(_listGraphRetentionTime.Where(g => g.Visible));
            listUpdateGraphs.AddRange(_listGraphPeakArea.Where(g => g.Visible));
            listUpdateGraphs.AddRange(_listGraphMassError.Where(g => g.Visible));
            if (_calibrationForm != null && _calibrationForm.Visible)
                listUpdateGraphs.Add(_calibrationForm);

            UpdateGraphPanes(listUpdateGraphs);
        }

        private void UpdateGraphPanes(ICollection<IUpdatable> graphPanes)
        {
            if (graphPanes.Count == 0)
                return;
            // Restart the timer at 100ms, giving the UI time to interrupt.
            _timerGraphs.Stop();
            _timerGraphs.Interval = 100;
            var previousGraphPanes = _timerGraphs.Tag as ICollection<IUpdatable>;
            if (previousGraphPanes != null && previousGraphPanes.Count > 0)
            {
                _timerGraphs.Tag = previousGraphPanes.Concat(graphPanes).Distinct().ToList();
            }
            else
            {
                _timerGraphs.Tag = graphPanes;
            }
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
                    throw new InvalidOperationException(
                        Resources.SkylineWindow_IsGraphUpdatePending_Must_be_called_from_event_thread);
                }
                if (_timerGraphs.Enabled)
                {
                    return true;
                }
                return false;
            }
        }

        public ChromFileInfoId AlignToFile
        {
            get { return _alignToFile; }
            set 
            { 
                if (ReferenceEquals(value, AlignToFile))
                {
                    return;
                }
                _alignToFile = value;
                UpdateGraphPanes();
            }
        }

        public bool AlignToRtPrediction
        {
            get { return null == AlignToFile && _alignToPrediction; }
            set
            {
                if (value == AlignToRtPrediction)
                {
                    return;
                }
                _alignToPrediction = value;
                if (_alignToPrediction)
                {
                    _alignToFile = null;
                }
                UpdateGraphPanes();
            }
        }

        public GraphValues.IRetentionTimeTransformOp GetRetentionTimeTransformOperation()
        {
            if (null != AlignToFile)
            {
                return GraphValues.AlignToFileOp.GetAlignmentToFile(AlignToFile, Document.Settings);
            }
            if (AlignToRtPrediction)
            {
                // Only align to regressions that are auto-calculated.  Otherwise,
                // conversion will be the same for all replicates, making this just
                // a linear unit conversion
                var predictRT = DocumentUI.Settings.PeptideSettings.Prediction.RetentionTime;
                if (predictRT != null && predictRT.IsAutoCalculated)
                {
                    return new GraphValues.RegressionUnconversion(predictRT);
                }
            }
            return null;
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

        private void fragmentsMenuItem_Click(object sender, EventArgs e)
        {
            _graphSpectrumSettings.ShowFragmentIons = !_graphSpectrumSettings.ShowFragmentIons;
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
            fragmentsMenuItem.Checked = fragmentionsContextMenuItem.Checked = set.ShowFragmentIons;
            precursorIonMenuItem.Checked = precursorIonContextMenuItem.Checked = set.ShowPrecursorIon;
            UpdateIonTypesMenuItemsVisibility();
        }

        // Update the Ion Types menu for document contents
        private void UpdateIonTypesMenuItemsVisibility()
        {
            aMenuItem.Visible = bMenuItem.Visible = cMenuItem.Visible = 
               xMenuItem.Visible = yMenuItem.Visible = zMenuItem.Visible = 
                  DocumentUI.DocumentType != SrmDocument.DOCUMENT_TYPE.small_molecules;

            fragmentsMenuItem.Visible = DocumentUI.HasSmallMolecules;
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

        private void editToolStripMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            bool canApply, canRemove;
            CanApplyOrRemovePeak(null, null, out canApply, out canRemove);
            if (!canApply && !canRemove)
            {
                integrationToolStripMenuItem.Enabled = false;
            }
            else
            {
                applyPeakAllToolStripMenuItem.Enabled = applyPeakSubsequentToolStripMenuItem.Enabled = canApply;
                removePeakToolStripMenuItem.Enabled = canRemove;
                integrationToolStripMenuItem.Enabled = true;
            }
        }

        private void CanApplyOrRemovePeak(ToolStripItemCollection removePeakItems, IsotopeLabelType labelType, out bool canApply, out bool canRemove)
        {
            canApply = canRemove = false;

            if (!DocumentUI.Settings.HasResults)
                return;

            var selectedTreeNode = SelectedNode as SrmTreeNode;
            var displayType = GraphChromatogram.GetDisplayType(DocumentUI, selectedTreeNode);
            if (displayType == DisplayTypeChrom.base_peak || displayType == DisplayTypeChrom.tic || displayType == DisplayTypeChrom.qc)
                return;
            ChromFileInfoId chromFileInfoId = GetSelectedChromFileId();

            var node = selectedTreeNode as TransitionTreeNode;
            if (node != null && GraphChromatogram.IsSingleTransitionDisplay)
            {
                if (HasPeak(SelectedResultsIndex, chromFileInfoId, node.DocNode))
                {
                    if (removePeakItems != null)
                        removePeakItems.Add(new ToolStripMenuItem());
                    canApply = canRemove = true;
                }
            }
            else if (selectedTreeNode is TransitionTreeNode && displayType == DisplayTypeChrom.all ||
                     selectedTreeNode is TransitionGroupTreeNode ||
                     selectedTreeNode is PeptideTreeNode && ((PeptideTreeNode)selectedTreeNode).DocNode.Children.Any())
            {
                canApply = true;

                var nodeGroupTree = SequenceTree.GetNodeOfType<TransitionGroupTreeNode>();
                var hasPeak = nodeGroupTree != null
                    ? HasPeak(SelectedResultsIndex, chromFileInfoId, nodeGroupTree.DocNode)
                    : SequenceTree.GetNodeOfType<PeptideTreeNode>().DocNode.TransitionGroups.Any(tranGroup => HasPeak(SelectedResultsIndex, chromFileInfoId, tranGroup));

                if (hasPeak)
                {
                    if (removePeakItems != null)
                        removePeakItems.Clear();
                    canRemove = true;

                    // Remove [IsotopeLabelType]
                    if (removePeakItems != null && labelType != null)
                    {
                        var peptideTreeNode = selectedTreeNode as PeptideTreeNode;
                        // only if multiple isotope label types
                        if (peptideTreeNode != null &&
                            peptideTreeNode.DocNode.TransitionGroups.Select(nodeTranGroup => nodeTranGroup.TransitionGroup.LabelType).Distinct().Count() > 1)
                        {
                            removePeakItems.Add(new ToolStripMenuItem {Tag = labelType});
                        }
                    }
                }
            }
        }

        public ChromFileInfoId GetSelectedChromFileId()
        {
            var document = Document;
            if (!document.Settings.HasResults || SelectedResultsIndex < 0 || SelectedResultsIndex >= document.Settings.MeasuredResults.Chromatograms.Count)
            {
                return null;
            }
            var graphChrom = GetGraphChrom(Document.MeasuredResults.Chromatograms[SelectedResultsIndex].Name);
            if (graphChrom == null)
            {
                return null;
            }
            return graphChrom.GetChromFileInfoId();
        }

        private void ranksMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.ShowRanks = !Settings.Default.ShowRanks;
            UpdateSpectrumGraph(false);
        }

        private void scoresContextMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.ShowLibraryScores = !Settings.Default.ShowLibraryScores;
            UpdateSpectrumGraph(false);
        }

        private void ionMzValuesContextMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.ShowIonMz = !Settings.Default.ShowIonMz;
            UpdateSpectrumGraph(false);
        }

        private void observedMzValuesContextMenuItem_Click(object sender, EventArgs e)
        {
            ToggleObservedMzValues();
        }

        public void ToggleObservedMzValues()
        {
            Settings.Default.ShowObservedMz = !Settings.Default.ShowObservedMz;
            UpdateSpectrumGraph(false);
        }

        void GraphSpectrum.IStateProvider.BuildSpectrumMenu(bool isProteomic, ZedGraphControl zedGraphControl, ContextMenuStrip menuStrip)
        {
            // Store original menuitems in an array, and insert a separator
            ToolStripItem[] items = new ToolStripItem[menuStrip.Items.Count];
            int iUnzoom = -1;
            for (int i = 0; i < items.Length; i++)
            {
                items[i] = menuStrip.Items[i];
                string tag = (string)items[i].Tag;
                if (tag == @"unzoom")
                    iUnzoom = i;
            }

            if (iUnzoom != -1)
                menuStrip.Items.Insert(iUnzoom, toolStripSeparator27);

            // Insert skyline specific menus
            var set = Settings.Default;
            int iInsert = 0;
            if (isProteomic)
            {
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
            }
            else
            {
                fragmentionsContextMenuItem.Checked = set.ShowFragmentIons;
                menuStrip.Items.Insert(iInsert++, fragmentionsContextMenuItem);
            }

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
            scoreContextMenuItem.Checked = set.ShowLibraryScores;
            menuStrip.Items.Insert(iInsert++, scoreContextMenuItem);
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

            // Need to test small mol
            if (isProteomic)
            {
                prositLibMatchItem.Checked = Settings.Default.Prosit;
                menuStrip.Items.Insert(iInsert++, prositLibMatchItem);
                mirrorMenuItem.Checked = Settings.Default.LibMatchMirror;
                menuStrip.Items.Insert(iInsert++, mirrorMenuItem);
                menuStrip.Items.Insert(iInsert++, toolStripSeparator61);
            }

            menuStrip.Items.Insert(iInsert++, spectrumPropsContextMenuItem);
            showLibraryChromatogramsSpectrumContextMenuItem.Checked = set.ShowLibraryChromatograms;
            menuStrip.Items.Insert(iInsert++, showLibraryChromatogramsSpectrumContextMenuItem);
            menuStrip.Items.Insert(iInsert, toolStripSeparator15);

            // Remove some ZedGraph menu items not of interest
            foreach (var item in items)
            {
                string tag = (string)item.Tag;
                if (tag == @"set_default" || tag == @"show_val")
                    menuStrip.Items.Remove(item);
            }
            CopyEmfToolStripMenuItem.AddToContextMenu(zedGraphControl, menuStrip);
        }

        private void duplicatesContextMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.ShowDuplicateIons = duplicatesContextMenuItem.Checked;
            UpdateSpectrumGraph(false);
        }

        private void lockYaxisContextMenuItem_Click(object sender, EventArgs e)
        {
            // Avoid updating the rest of the graph just to change the y-axis lock state
            _graphSpectrum.LockYAxis(Settings.Default.LockYAxis = lockYaxisContextMenuItem.Checked);
        }

        private void showChromatogramsSpectrumContextMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.ShowLibraryChromatograms = !Settings.Default.ShowLibraryChromatograms;
            UpdateGraphPanes();
        }

        private void spectrumPropsContextMenuItem_Click(object sender, EventArgs e)
        {
            ShowSpectrumProperties();
        }

        public void ShowSpectrumProperties()
        {
            using (var dlg = new SpectrumChartPropertyDlg())
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    UpdateSpectrumGraph(false);
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
                    _graphSpectrum.Show(dockPanel, DockState.DockRight);
                    _graphSpectrum.Focus();
                }
                else
                {
                    _graphSpectrum = CreateGraphSpectrum();
                    _graphSpectrum.Show(dockPanel, DockState.DockRight);
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
        public bool IsGraphSpectrumVisible
        {
            get { return _graphSpectrum != null && _graphSpectrum.Visible; }
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
                _graphSpectrum.HideOnClose = false;
                _graphSpectrum.Close();
                _graphSpectrum = null;
            }
        }

        private void graphSpectrum_VisibleChanged(object sender, EventArgs e)
        {
            if (_graphSpectrum != null)
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
            if (DocumentUI.Settings.HasResults &&
                (DocumentUI.Settings.TransitionSettings.FullScan.IsEnabled || DocumentUI.Settings.PeptideSettings.Libraries.HasMidasLibrary))
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

        private void UpdateSpectrumGraph(bool selectionChanged)
        {
            if (_graphSpectrum != null)
                _graphSpectrum.UpdateUI(selectionChanged);
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

        IList<IonType> GraphSpectrum.IStateProvider.ShowIonTypes(bool isProteomic)
        {
            return _graphSpectrumSettings.ShowIonTypes(isProteomic); 
        }

        private void CheckIonTypes(IEnumerable<IonType> types, bool check, bool visible)
        {
            foreach (var type in types)
                CheckIonType(type, check, visible);
        }

        private void CheckIonType(IonType type, bool check, bool visible)
        {
            var set = Settings.Default;
            switch (type)
            {
                case IonType.a: set.ShowAIons = aMenuItem.Checked = check; aMenuItem.Visible = visible; break;
                case IonType.b: set.ShowBIons = bMenuItem.Checked = check; bMenuItem.Visible = visible; break;
                case IonType.c: set.ShowCIons = cMenuItem.Checked = check; cMenuItem.Visible = visible; break;
                case IonType.x: set.ShowXIons = xMenuItem.Checked = check; xMenuItem.Visible = visible; break;
                case IonType.y: set.ShowYIons = yMenuItem.Checked = check; yMenuItem.Visible = visible; break;
                case IonType.z: set.ShowZIons = zMenuItem.Checked = check; zMenuItem.Visible = visible; break;
                case IonType.custom: set.ShowFragmentIons = fragmentsMenuItem.Checked = check; fragmentsMenuItem.Visible = visible; break;
            }
        }

        // N.B. we're interested in the absolute value of charge here, so output list may be shorter than input list
        // CONSIDER(bspratt): we may want finer adduct-level control for small molecule use
        IList<int> GraphSpectrum.IStateProvider.ShowIonCharges(IEnumerable<Adduct> adductPriority)
        {
            return _graphSpectrumSettings.ShowIonCharges(adductPriority);
        }

        private void CheckIonCharges(IEnumerable<Adduct> charges, bool check)
        {
            foreach (var charge in charges)
                CheckIonCharge(charge, check);
        }

        private void CheckIonCharge(Adduct adduct, bool check)
        {
            // Set charge settings without causing UI to update
            var set = Settings.Default;
            switch (Math.Abs(adduct.AdductCharge))  // TODO(bspratt) - need a lot more flexibility here, neg charges, M+Na etc
            {
                case 1: set.ShowCharge1 = charge1MenuItem.Checked = check; break;
                case 2: set.ShowCharge2 = charge2MenuItem.Checked = check; break;
                case 3: set.ShowCharge3 = charge3MenuItem.Checked = check; break;
                case 4: set.ShowCharge4 = charge4MenuItem.Checked = check; break;
            }
        }

        public void HideFullScanGraph()
        {
            if (_graphFullScan != null)
                _graphFullScan.Hide();
        }

        private void ShowGraphFullScan(IScanProvider scanProvider, int transitionIndex, int scanIndex)
        {
            if (_graphFullScan != null)
            {
                _graphFullScan.Activate();
                _graphFullScan.Focus();
            }
            else
            {
                _graphFullScan = CreateGraphFullScan();

                // Choose a position to float the window
                var rectFloat = GetFloatingRectangleForNewWindow();
                _graphFullScan.Show(dockPanel, rectFloat);
            }

            _graphFullScan.ShowSpectrum(scanProvider, transitionIndex, scanIndex);
        }

        // Testing
        public GraphFullScan GraphFullScan
        {
            get { return _graphFullScan; }
        }

        private GraphFullScan CreateGraphFullScan()
        {
            // Create a new spectrum graph
            _graphFullScan = new GraphFullScan(this);
            _graphFullScan.UpdateUI();
            _graphFullScan.FormClosed += graphFullScan_FormClosed;
            _graphFullScan.VisibleChanged += graphFullScan_VisibleChanged;
            _graphFullScan.SelectedScanChanged += graphFullScan_SelectedScanChanged;
            return _graphFullScan;
        }

        private void DestroyGraphFullScan()
        {
            if (_graphFullScan != null)
            {
                _graphFullScan.FormClosed -= graphFullScan_FormClosed;
                _graphFullScan.VisibleChanged -= graphFullScan_VisibleChanged;
                _graphFullScan.SelectedScanChanged -= graphFullScan_SelectedScanChanged;
                _graphFullScan.HideOnClose = false;
                _graphFullScan.Close();
                _graphFullScan = null;
            }
        }

        private void graphFullScan_VisibleChanged(object sender, EventArgs e)
        {
            if (_graphFullScan != null)
                Settings.Default.ShowFullScan = _graphFullScan.Visible;
        }

        private void graphFullScan_FormClosed(object sender, FormClosedEventArgs e)
        {
            // Update settings and menu check
            Settings.Default.ShowFullScan = false;
            _graphFullScan = null;
        }

        private void graphFullScan_SelectedScanChanged(object sender, SelectedScanEventArgs e)
        {
            SelectedScanFile = e.DataFile;
            SelectedScanRetentionTime = e.RetentionTime;
            SelectedScanTransition = e.TransitionId;
            UpdateChromGraphs();
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
            menu.DropDown.SuspendLayout();
            try
            {
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
            finally
            {
                menu.DropDown.ResumeLayout();
            }
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

        public PeptideGraphInfo GetPeptideGraphInfo(DocNode docNode)
        {
            return SequenceTree.GetPeptideGraphInfo(docNode);
        }

        void GraphChromatogram.IStateProvider.BuildChromatogramMenu(ZedGraphControl zedGraphControl, PaneKey paneKey, ContextMenuStrip menuStrip, ChromFileInfoId chromFileInfoId)
        {
            // Store original menuitems in an array, and insert a separator
            ToolStripItem[] items = new ToolStripItem[menuStrip.Items.Count];
            int iUnzoom = -1;
            for (int i = 0; i < items.Length; i++)
            {
                items[i] = menuStrip.Items[i];
                string tag = (string)items[i].Tag;
                if (tag == @"unzoom")
                    iUnzoom = i;
            }

            if (iUnzoom != -1)
                menuStrip.Items.Insert(iUnzoom, toolStripSeparator26);

            // Insert skyline specific menus
            var set = Settings.Default;
            int iInsert = 0;

            var settings = DocumentUI.Settings;
            bool retentionPredict = (settings.PeptideSettings.Prediction.RetentionTime != null);
            bool peptideIdTimes = (settings.PeptideSettings.Libraries.HasLibraries &&
                                   (settings.TransitionSettings.FullScan.IsEnabled || settings.PeptideSettings.Libraries.HasMidasLibrary));
            bool canApply, canRemove;
            CanApplyOrRemovePeak(removePeakGraphMenuItem.DropDownItems, paneKey.IsotopeLabelType, out canApply, out canRemove);
            if (canApply || canRemove)
            {
                if (canApply)
                {
                    menuStrip.Items.Insert(iInsert++, applyPeakAllGraphMenuItem);
                    menuStrip.Items.Insert(iInsert++, applyPeakSubsequentGraphMenuItem);
                }
                if (canRemove)
                {
                    menuStrip.Items.Insert(iInsert++, removePeakGraphMenuItem);
                }
                menuStrip.Items.Insert(iInsert++, toolStripSeparator33);
            }
            legendChromContextMenuItem.Checked = set.ShowChromatogramLegend;
            menuStrip.Items.Insert(iInsert++, legendChromContextMenuItem);
            var fullScan = Document.Settings.TransitionSettings.FullScan;
            if (ChromatogramCache.FORMAT_VERSION_CACHE > ChromatogramCache.FORMAT_VERSION_CACHE_4
                    && fullScan.IsEnabled
                    && (fullScan.IsHighResPrecursor || fullScan.IsHighResProduct))
            {
                massErrorContextMenuItem.Checked = set.ShowMassError;
                menuStrip.Items.Insert(iInsert++, massErrorContextMenuItem);
            }
            peakBoundariesContextMenuItem.Checked = set.ShowPeakBoundaries;
            menuStrip.Items.Insert(iInsert++, peakBoundariesContextMenuItem);

            originalPeakMenuItem.Checked = set.ShowOriginalPeak;
            menuStrip.Items.Insert(iInsert++, originalPeakMenuItem);

            menuStrip.Items.Insert(iInsert++, retentionTimesContextMenuItem);
            if (retentionTimesContextMenuItem.DropDownItems.Count == 0)
            {
                retentionTimesContextMenuItem.DropDownItems.AddRange(new ToolStripItem[]
                    {
                        allRTContextMenuItem,
                        bestRTContextMenuItem,
                        thresholdRTContextMenuItem,
                        noneRTContextMenuItem,
                        rawTimesMenuItemSplitter,
                        rawTimesContextMenuItem
                    });
            }
            if (retentionPredict)
            {
                retentionTimePredContextMenuItem.Checked = set.ShowRetentionTimePred;
                menuStrip.Items.Insert(iInsert++, retentionTimePredContextMenuItem);
            }
            rawTimesContextMenuItem.Checked = set.ChromShowRawTimes;
            bool alignedTimes = settings.HasAlignedTimes();
            bool unalignedTimes = settings.HasUnalignedTimes();
            if (peptideIdTimes || alignedTimes || unalignedTimes)
            {
                menuStrip.Items.Insert(iInsert++, peptideIDTimesContextMenuItem);
                peptideIDTimesContextMenuItem.DropDownItems.Clear();
                idTimesNoneContextMenuItem.Checked = false;
                peptideIDTimesContextMenuItem.DropDownItems.Add(idTimesNoneContextMenuItem);
                if (peptideIdTimes)
                {
                    idTimesMatchingContextMenuItem.Checked = set.ShowPeptideIdTimes;
                    peptideIDTimesContextMenuItem.DropDownItems.Add(idTimesMatchingContextMenuItem);
                }
                if (settings.HasAlignedTimes())
                {
                    idTimesAlignedContextMenuItem.Checked = set.ShowAlignedPeptideIdTimes;
                    peptideIDTimesContextMenuItem.DropDownItems.Add(idTimesAlignedContextMenuItem);
                }
                if (settings.HasUnalignedTimes())
                {
                    
                    idTimesOtherContextMenuItem.Checked = set.ShowUnalignedPeptideIdTimes;
                    peptideIDTimesContextMenuItem.DropDownItems.Add(idTimesOtherContextMenuItem);
                }
                idTimesNoneContextMenuItem.Checked = !peptideIDTimesContextMenuItem.DropDownItems
                                                                                   .Cast<ToolStripMenuItem>()
                                                                                   .Any(idItem => idItem.Checked);
            }
            menuStrip.Items.Insert(iInsert++, toolStripSeparator16);
            AddTransitionContextMenu(menuStrip, iInsert++);
            menuStrip.Items.Insert(iInsert++, transformChromContextMenuItem);
            // Sometimes child menuitems are stripped from the parent
            if (transformChromContextMenuItem.DropDownItems.Count == 0)
            {
                transformChromContextMenuItem.DropDownItems.AddRange(new ToolStripItem[]
                    {
                        transformChromNoneContextMenuItem,
                        transformChromInterpolatedContextMenuItem,
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
            iInsert = InsertAlignmentMenuItems(menuStrip.Items, chromFileInfoId, iInsert);
            menuStrip.Items.Insert(iInsert++, toolStripSeparator18);
            menuStrip.Items.Insert(iInsert++, chromPropsContextMenuItem);
            menuStrip.Items.Insert(iInsert, toolStripSeparator19);

            // Remove some ZedGraph menu items not of interest
            foreach (var item in items)
            {
                string tag = (string)item.Tag;
                if (tag == @"set_default" || tag == @"show_val")
                    menuStrip.Items.Remove(item);
            }
            CopyEmfToolStripMenuItem.AddToContextMenu(zedGraphControl, menuStrip);
        }

        private void AddTransitionContextMenu(ToolStrip menuStrip, int iInsert)
        {
            menuStrip.Items.Insert(iInsert, transitionsContextMenuItem);
            // Sometimes child menuitems are stripped from the parent
            if (transitionsContextMenuItem.DropDownItems.Count == 0)
            {
                transitionsContextMenuItem.DropDownItems.AddRange(new ToolStripItem[]
                {
                    allTranContextMenuItem,
                    precursorsTranContextMenuItem,
                    productsTranContextMenuItem,
                    singleTranContextMenuItem,
                    totalTranContextMenuItem,
                    toolStripSeparatorTran,
                    basePeakContextMenuItem,
                    ticContextMenuItem,
                    qcContextMenuItem,
                    toolStripSeparatorOnlyQuantitative,
                    onlyQuantitativeContextMenuItem,
                    toolStripSeparatorSplitGraph,
                    splitGraphContextMenuItem,
                });
            }
        }

// ReSharper disable SuggestBaseTypeForParameter
        private static bool HasPeak(int iResult, ChromFileInfoId chromFileInfoId, TransitionGroupDocNode nodeGroup)
// ReSharper restore SuggestBaseTypeForParameter
        {
            foreach (TransitionDocNode nodeTran in nodeGroup.Children)
            {
                if (HasPeak(iResult, chromFileInfoId, nodeTran))
                    return true;
            }
            return false;
        }

        private static bool HasPeak(int iResults, ChromFileInfoId chromFileInfoId, TransitionDocNode nodeTran)
        {
            var chromInfo = GetTransitionChromInfo(nodeTran, iResults, chromFileInfoId);
            return (chromInfo != null && !chromInfo.IsEmpty);
        }

        private void legendChromContextMenuItem_Click(object sender, EventArgs e)
        {
            ShowChromatogramLegends(legendChromContextMenuItem.Checked);
        }

        public void ShowChromatogramLegends(bool show)
        {
            Settings.Default.ShowChromatogramLegend = show;
            UpdateChromGraphs();
        }

        private void massErrorContextMenuItem_Click(object sender, EventArgs e)
        {
            ShowMassErrors(massErrorContextMenuItem.Checked);
        }

        public void ShowMassErrors(bool show)
        {
            Settings.Default.ShowMassError = show;
            UpdateChromGraphs();
        }

        private void peakBoundariesContextMenuItem_Click(object sender, EventArgs e)
        {
            ShowPeakBoundaries(peakBoundariesContextMenuItem.Checked);
        }

        private void originalPeakContextMenuItem_Click(object sender, EventArgs e)
        {
            ShowOriginalPeak(originalPeakMenuItem.Checked);
        }

        public void ShowPeakBoundaries(bool show)
        {
            Settings.Default.ShowPeakBoundaries = show;
            UpdateChromGraphs();
        }

        public void ShowOriginalPeak(bool show)
        {
            Settings.Default.ShowOriginalPeak = show;
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
            ShowChromatogramRTThresholdDlg();
        }

        public void ShowChromatogramRTThresholdDlg()
        {
            using (var dlg = new ChromatogramRTThresholdDlg())
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

        private void rawTimesContextMenuItem_Click(object sender, EventArgs e)
        {
            ToggleRawTimesMenuItem();
        }

        public void ToggleRawTimesMenuItem()
        {
            Settings.Default.ChromShowRawTimes = !Settings.Default.ChromShowRawTimes;
            UpdateChromGraphs();
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
            ShowPeptideIDTimes(idTimesMatchingContextMenuItem.Checked);
        }

        public void ShowPeptideIDTimes(bool show)
        {
            Settings.Default.ShowPeptideIdTimes = show;
            UpdateChromGraphs();
        }

        private void alignedPeptideIDTimesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowAlignedPeptideIDTimes(idTimesAlignedContextMenuItem.Checked);
        }

        public void ShowAlignedPeptideIDTimes(bool show)
        {
            Settings.Default.ShowAlignedPeptideIdTimes = show;
            UpdateChromGraphs();
        }

        private void peptideIDTimesFromOtherRunsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowOtherRunPeptideIDTimes(idTimesOtherContextMenuItem.Checked);
        }

        public void ShowOtherRunPeptideIDTimes(bool show)
        {
            Settings.Default.ShowUnalignedPeptideIdTimes = show;
            UpdateChromGraphs();
        }

        private void idTimesNoneContextMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.ShowPeptideIdTimes =
                Settings.Default.ShowAlignedPeptideIdTimes =
                Settings.Default.ShowUnalignedPeptideIdTimes = false;
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
            var displayType = GraphChromatogram.DisplayType;

            // If both MS1 and MS/MS ions are not possible, then menu items to differentiate precursors and
            // products are not necessary.
            bool showIonTypeOptions = IsMultipleIonSources;
            precursorsTranMenuItem.Visible =
                precursorsTranContextMenuItem.Visible =
                productsTranMenuItem.Visible =
                productsTranContextMenuItem.Visible = showIonTypeOptions;

            if (!showIonTypeOptions &&
                    (displayType == DisplayTypeChrom.precursors || displayType == DisplayTypeChrom.products))
                displayType = DisplayTypeChrom.all;

            // Only show all ions chromatogram options when at least one chromatogram of this type exists
            bool showAllIonsOptions = DocumentUI.Settings.HasResults &&
                DocumentUI.Settings.MeasuredResults.HasAllIonsChromatograms;
            basePeakMenuItem.Visible =
                basePeakContextMenuItem.Visible =
                ticMenuItem.Visible =
                ticContextMenuItem.Visible =
                qcMenuItem.Visible =
                qcContextMenuItem.Visible =
                toolStripSeparatorTranMain.Visible =
                toolStripSeparatorTran.Visible = showAllIonsOptions;

            if (!showAllIonsOptions &&
                    (displayType == DisplayTypeChrom.base_peak || displayType == DisplayTypeChrom.tic || displayType == DisplayTypeChrom.qc))
                displayType = DisplayTypeChrom.all;

            if (showAllIonsOptions)
            {
                qcMenuItem.DropDownItems.Clear();
                qcContextMenuItem.DropDownItems.Clear();
                var qcTraceNames = DocumentUI.MeasuredResults.QcTraceNames.ToList();
                if (qcTraceNames.Count > 0)
                {
                    var qcTraceItems = new ToolStripItem[qcTraceNames.Count];
                    var qcContextTraceItems = new ToolStripItem[qcTraceNames.Count];
                    for (int i = 0; i < qcTraceNames.Count; i++)
                    {
                        qcTraceItems[i] = new ToolStripMenuItem(qcTraceNames[i], null, qcMenuItem_Click)
                        {
                            Checked = displayType == DisplayTypeChrom.qc &&
                                      Settings.Default.ShowQcTraceName == qcTraceNames[i]
                        };
                        qcContextTraceItems[i] = new ToolStripMenuItem(qcTraceNames[i], null, qcMenuItem_Click)
                        {
                            Checked = displayType == DisplayTypeChrom.qc &&
                                      Settings.Default.ShowQcTraceName == qcTraceNames[i]
                        };
                    }

                    qcMenuItem.DropDownItems.AddRange(qcTraceItems);
                    qcContextMenuItem.DropDownItems.AddRange(qcContextTraceItems);
                }
                else
                    qcMenuItem.Visible = qcContextMenuItem.Visible = false;
            }

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
            basePeakMenuItem.Checked = basePeakContextMenuItem.Checked =
                (displayType == DisplayTypeChrom.base_peak);
            ticMenuItem.Checked = ticContextMenuItem.Checked =
                (displayType == DisplayTypeChrom.tic);
            splitGraphMenuItem.Checked = splitGraphContextMenuItem.Checked
                = Settings.Default.SplitChromatogramGraph;
            onlyQuantitativeContextMenuItem.Checked = onlyQuantitativeContextMenuItem.Checked 
                = Settings.Default.ShowQuantitativeOnly;
        }

        private bool IsMultipleIonSources
        {
            get
            {
                var nodeTreePep = SequenceTree.GetNodeOfType<PeptideTreeNode>();
                if (nodeTreePep == null)
                    return false;
                var fullScan = DocumentUI.Settings.TransitionSettings.FullScan;
                return nodeTreePep.DocNode.TransitionGroups.Contains(
                    nodeGroup => GraphChromatogram.IsMultipleIonSources(fullScan, nodeGroup));
            }
        }

        private void removePeakMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            bool canApply, canRemove;
            CanApplyOrRemovePeak(null, null, out canApply, out canRemove);
            if (!canRemove)
                return;

            var menu = sender as ToolStripMenuItem;
            if (menu == null || !menu.DropDownItems.OfType<object>().Any())
                return;

            var nodeGroupTree = SequenceTree.GetNodeOfType<TransitionGroupTreeNode>();
            if (nodeGroupTree != null)
            {
                var nodeGroup = nodeGroupTree.DocNode;
                var pathGroup = nodeGroupTree.Path;
                var nodeTranTree = (TransitionTreeNode)SelectedNode;
                var nodeTran = nodeTranTree.DocNode;

                menu.DropDownItems.Clear();

                if (nodeGroup.TransitionCount > 1)
                {
                    var handler = new RemovePeakHandler(this, pathGroup, nodeGroup, null);
                    var item = new ToolStripMenuItem(Resources.SkylineWindow_removePeaksGraphMenuItem_DropDownOpening_All, null, handler.menuItem_Click);
                    menu.DropDownItems.Insert(0, item);
                }

                var chromInfo = GetTransitionChromInfo(nodeTran, SequenceTree.ResultsIndex, GetSelectedChromFileId());
                if (chromInfo != null && !chromInfo.IsEmpty)
                {
                    var handler = new RemovePeakHandler(this, pathGroup, nodeGroup, nodeTran);
                    var item = new ToolStripMenuItem(ChromGraphItem.GetTitle(nodeTran), null, handler.menuItem_Click);
                    menu.DropDownItems.Insert(0, item);
                }
                return;
            }

            var nodePepTree = SequenceTree.GetNodeOfType<PeptideTreeNode>();
            if (nodePepTree != null)
            {
                var placeholder = menu.DropDownItems.OfType<object>().FirstOrDefault() as ToolStripMenuItem;
                if (placeholder == null)
                    return;

                var isotopeLabelType = placeholder.Tag as IsotopeLabelType;
                if (isotopeLabelType == null)
                    return;

                menu.DropDownItems.Clear();

                var transitionGroupDocNode = nodePepTree.DocNode.TransitionGroups.FirstOrDefault(transitionGroup => Equals(transitionGroup.TransitionGroup.LabelType, isotopeLabelType));
                if (transitionGroupDocNode == null)
                    return;

                var item = new ToolStripMenuItem(Resources.SkylineWindow_removePeaksGraphMenuItem_DropDownOpening_All, null, removePeakMenuItem_Click);
                menu.DropDownItems.Insert(0, item);

                var handler = new RemovePeakHandler(this, new IdentityPath(nodePepTree.Path, transitionGroupDocNode.Id), transitionGroupDocNode, null);
                item = new ToolStripMenuItem(isotopeLabelType.Title, null, handler.menuItem_Click);
                menu.DropDownItems.Insert(0, item);
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

        private void applyPeakAllMenuItem_Click(object sender, EventArgs e)
        {
            ApplyPeak(false);
        }

        private void applyPeakSubsequentMenuItem_Click(object sender, EventArgs e)
        {
            ApplyPeak(true);
        }

        public void ApplyPeak(bool subsequent)
        {
            bool canApply, canRemove;
            CanApplyOrRemovePeak(null, null, out canApply, out canRemove);
            if (!canApply)
                return;

            var nodePepTree = SequenceTree.GetNodeOfType<PeptideTreeNode>();
            var nodeTranGroupTree = SequenceTree.GetNodeOfType<TransitionGroupTreeNode>();
            var nodeTranGroup = nodeTranGroupTree != null ? nodeTranGroupTree.DocNode : null;

            using (var longWait = new LongWaitDlg(this) { Text = Resources.SkylineWindow_ApplyPeak_Applying_Peak })
            {
                SrmDocument doc = null;
                try
                {
                    var resultsIndex = SelectedResultsIndex;
                    var resultsFile = GetGraphChrom(Document.MeasuredResults.Chromatograms[resultsIndex].Name).GetChromFileInfoId();
                    longWait.PerformWork(this, 800, monitor => doc = PeakMatcher.ApplyPeak(Document, nodePepTree, ref nodeTranGroup, resultsIndex, resultsFile, subsequent, monitor));
                }
                catch (Exception x)
                {
                    MessageDlg.ShowWithException(this, TextUtil.LineSeparate(Resources.SkylineWindow_ApplyPeak_Failed_to_apply_peak_, x.Message), x);
                }

                if (!longWait.IsCanceled && doc != null && !ReferenceEquals(doc, Document))
                {
                    // ReSharper disable once PossibleNullReferenceException
                    var path = PropertyName.ROOT
                        .SubProperty(((PeptideGroupTreeNode) nodePepTree.SrmParent).DocNode.AuditLogText)
                        .SubProperty(nodePepTree.DocNode.AuditLogText)
                        .SubProperty(nodeTranGroup.AuditLogText);

                    var msg = subsequent ? MessageType.applied_peak_subsequent : MessageType.applied_peak_all;

                    ModifyDocument(Resources.SkylineWindow_PickPeakInChromatograms_Apply_picked_peak, document => doc,
                        docPair => AuditLogEntry.CreateSimpleEntry(msg, docPair.NewDocumentType, path.ToString()));
                }
            }
        }

        private void removePeakMenuItem_Click(object sender, EventArgs e)
        {
            var menu = sender as ToolStripMenuItem;
            if (menu == null || menu.DropDownItems.OfType<object>().Any())
                return;
            bool removePeakByContextMenu = menu == removePeakContextMenuItem;

            RemovePeak(removePeakByContextMenu);
        }

        public void RemovePeak(bool removePeakByContextMenu = false)
        {
            bool canApply, canRemove;
            var chromFileInfoId = GetSelectedChromFileId();
            CanApplyOrRemovePeak(null, null, out canApply, out canRemove);
            if (!canRemove)
                return;

            var nodeGroupTree = SequenceTree.GetNodeOfType<TransitionGroupTreeNode>();
            var nodeGroups = new List<Tuple<TransitionGroupDocNode, IdentityPath>>();
            var nodePepTree = SelectedNode as PeptideTreeNode;
            if (nodeGroupTree != null)
            {
                nodeGroups.Add(new Tuple<TransitionGroupDocNode, IdentityPath>(nodeGroupTree.DocNode, nodeGroupTree.Path));
            }
            else if (nodePepTree != null && nodePepTree.Nodes.OfType<object>().Any())
            {
                nodeGroups.AddRange(from TransitionGroupDocNode tranGroup in nodePepTree.DocNode.Children
                    select
                    new Tuple<TransitionGroupDocNode, IdentityPath>(tranGroup, new IdentityPath(nodePepTree.Path, tranGroup.Id)));
            }
            else
            {
                return;
            }

            TransitionDocNode nodeTran = null;
            if (removePeakByContextMenu)
            {
                var nodeTranTree = SelectedNode as TransitionTreeNode;
                if (nodeTranTree != null)
                {
                    nodeTran = nodeTranTree.DocNode;
                }
            }

            if (nodeGroups.Count == 1)
            {
                var nodeGroup = nodeGroups.First();
                RemovePeak(nodeGroup.Item2, nodeGroup.Item1, nodeTran);
            }
            else
            {
                // ReSharper disable once PossibleNullReferenceException
                ModifyDocument(string.Format(Resources.SkylineWindow_removePeakContextMenuItem_Click_Remove_all_peaks_from__0_, nodePepTree.DocNode.ModifiedSequenceDisplay),
                    document => nodeGroups.Aggregate(Document,
                            (doc, nodeGroup) => RemovePeakInternal(doc, SelectedResultsIndex, chromFileInfoId, nodeGroup.Item2, nodeGroup.Item1, nodeTran)),
                    docPair =>
                    {
                        var peptideGroup = ((PeptideGroupTreeNode) nodePepTree.SrmParent).DocNode;
                        var name = PropertyName.ROOT.SubProperty(peptideGroup.AuditLogText)
                            .SubProperty(nodePepTree.DocNode.AuditLogText);
                        return AuditLogEntry.CreateSimpleEntry(MessageType.removed_all_peaks_from, docPair.OldDocumentType, name,
                            docPair.OldDoc.MeasuredResults.Chromatograms[SelectedResultsIndex].Name);
                    });
            }
        }

        public void RemovePeak(IdentityPath groupPath, TransitionGroupDocNode nodeGroup, TransitionDocNode nodeTran)
        {
            string message = nodeTran == null
                ? string.Format(Resources.SkylineWindow_RemovePeak_Remove_all_peaks_from__0__, ChromGraphItem.GetTitle(nodeGroup))
                : string.Format(Resources.SkylineWindow_RemovePeak_Remove_peak_from__0__, ChromGraphItem.GetTitle(nodeTran));
            var chromFileInfoId = GetSelectedChromFileId();
            ModifyDocument(message, doc => RemovePeakInternal(doc, SelectedResultsIndex, chromFileInfoId, groupPath, nodeGroup, nodeTran),
                docPair =>
                {
                    var msg = nodeTran == null ? MessageType.removed_all_peaks_from : MessageType.removed_peak_from;

                    var peptide = (PeptideDocNode) docPair.OldDoc.FindNode(groupPath.Parent);
                    var peptideGroup = (PeptideGroupDocNode) docPair.OldDoc.FindNode(groupPath.Parent.Parent);

                    var name = PropertyName.ROOT.SubProperty(peptideGroup.AuditLogText)
                        .SubProperty(peptide.AuditLogText).SubProperty(nodeGroup.AuditLogText);
                    if (nodeTran != null)
                        name = name.SubProperty(nodeTran.AuditLogText);

                    return AuditLogEntry.CreateSimpleEntry(msg, docPair.OldDocumentType, name,
                        docPair.OldDoc.MeasuredResults.Chromatograms[SelectedResultsIndex].Name);
                });
        }

        private SrmDocument RemovePeakInternal(SrmDocument document, int resultsIndex, ChromFileInfoId chromFileInfoId, IdentityPath groupPath,
            TransitionGroupDocNode nodeGroup, TransitionDocNode nodeTran)
        {
            ChromInfo chromInfo;
            Transition transition;

            if (nodeTran == null)
            {
                chromInfo = GetTransitionGroupChromInfo(nodeGroup, resultsIndex, chromFileInfoId);
                transition = null;
            }
            else
            {
                chromInfo = GetTransitionChromInfo(nodeTran, resultsIndex, chromFileInfoId);
                transition = nodeTran.Transition;
            }
            if (chromInfo == null)
                return document;

            MsDataFileUri filePath;
            string name = GetGraphChromStrings(resultsIndex, chromInfo.FileId, out filePath);
            return name == null
                ? document
                : document.ChangePeak(groupPath, name, filePath, transition, 0, 0, UserSet.TRUE, PeakIdentification.FALSE, false);
        }

        private static TransitionGroupChromInfo GetTransitionGroupChromInfo(TransitionGroupDocNode nodeGroup, int iResults, ChromFileInfoId chromFileInfoId)
        {
            return nodeGroup.GetChromInfo(iResults, chromFileInfoId);
        }

        private static TransitionChromInfo GetTransitionChromInfo(TransitionDocNode nodeTran, int iResults, ChromFileInfoId chromFileInfoId)
        {
            return nodeTran.GetChromInfo(iResults, chromFileInfoId);
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

        private void basePeakMenuItem_Click(object sender, EventArgs e)
        {
            ShowBasePeak();
        }

        public void ShowBasePeak()
        {
            SetDisplayTypeChrom(DisplayTypeChrom.base_peak);
        }

        private void ticMenuItem_Click(object sender, EventArgs e)
        {
            ShowTic();
        }

        public void ShowTic()
        {
            SetDisplayTypeChrom(DisplayTypeChrom.tic);
        }

        private void qcMenuItem_Click(object sender, EventArgs e)
        {
            var qcTraceItem = sender as ToolStripMenuItem;
            if (qcTraceItem == null)
                throw new InvalidOperationException(@"qcMenuItem_Click must be triggered by a ToolStripMenuItem");
            ShowQc(qcTraceItem.Text);
        }

        public void ShowQc(string qcTraceName)
        {
            Settings.Default.ShowQcTraceName = qcTraceName;
            SetDisplayTypeChrom(DisplayTypeChrom.qc);
        }

        public void SetDisplayTypeChrom(DisplayTypeChrom displayType)
        {
            Settings.Default.ShowTransitionGraphs = displayType.ToString();
            UpdateChromGraphs();
            UpdateSpectrumGraph(false);
            UpdateRetentionTimeGraph();
            UpdatePeakAreaGraph();
            UpdateMassErrorGraph();
        }

        private void transformChromMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            var transform = GraphChromatogram.Transform;

            transformChromNoneMenuItem.Checked = transformChromNoneContextMenuItem.Checked =
                (transform == TransformChrom.raw);
            transformChromInterploatedMenuItem.Checked = transformChromInterpolatedContextMenuItem.Checked =
                (transform == TransformChrom.interpolated);
            secondDerivativeMenuItem.Checked = secondDerivativeContextMenuItem.Checked =
                (transform == TransformChrom.craw2d);
            firstDerivativeMenuItem.Checked = firstDerivativeContextMenuItem.Checked =
                (transform == TransformChrom.craw1d);
            smoothSGChromMenuItem.Checked = smoothSGChromContextMenuItem.Checked =
                (transform == TransformChrom.savitzky_golay);
        }

        private void transformChromNoneMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.TransformTypeChromatogram = TransformChrom.raw.ToString();
            UpdateChromGraphs();
        }


        private void transformInterpolatedMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.TransformTypeChromatogram = TransformChrom.interpolated.ToString();
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
            LockYChrom(lockYChromContextMenuItem.Checked);
        }

        public void LockYChrom (bool locked)
        {
            bool lockY = Settings.Default.LockYChrom = locked;
            // Avoid updating the rest of the chart just to change the y-axis lock state
            foreach (var chromatogram in _listGraphChrom)
                chromatogram.LockYAxis(lockY);
        }

        private void synchronizeZoomingContextMenuItem_Click(object sender, EventArgs e)
        {
            SynchronizeZooming(synchronizeZoomingContextMenuItem.Checked);
        }

        public void SynchronizeZooming(bool isChecked)
        {
            bool zoomAll = Settings.Default.AutoZoomAllChromatograms = isChecked;

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
            ShowChromatogramProperties();
        }

        public void ShowChromatogramProperties()
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
                if (_listGraphChrom.Count >= MAX_GRAPH_CHROM)
                {
                    // List is too long, re-purpose least recently used
                    graphChrom = _listGraphChrom[0];
                    graphChrom.ChangeChromatogram(name);
                    graphChrom.Activate();
                    graphChrom.Visible = true;
                    graphChrom.Focus();
                }
                else
                {
                    graphChrom = CreateGraphChrom(name, SelectedGraphChromName, false);
                }
            }

            if (show)
            {
                // Move this to end of MRU so it's seen as most recent
                _listGraphChrom.Remove(graphChrom);
                _listGraphChrom.Add(graphChrom);
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

        public string SelectedGraphChromName
        {
            get
            {
                MsDataFileUri temp;
                return GetGraphChromStrings(SelectedResultsIndex, null, out temp);
            }
        }

        private string GetGraphChromStrings(int iResult, ChromFileInfoId fileId, out MsDataFileUri filePath)
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
            var graphChrom = new GraphChromatogram(this, this, name);
            graphChrom.FormClosed += graphChromatogram_FormClosed;
            graphChrom.PickedPeak += graphChromatogram_PickedPeak;
            graphChrom.ClickedChromatogram += graphChromatogram_ClickedChromatogram;
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
            graphChrom.ClickedChromatogram -= graphChromatogram_ClickedChromatogram;
            graphChrom.ChangedPeakBounds -= graphChromatogram_ChangedPeakBounds;
            graphChrom.PickedSpectrum -= graphChromatogram_PickedSpectrum;
            graphChrom.ZoomAll -= graphChromatogram_ZoomAll;
            graphChrom.HideOnClose = false;
            graphChrom.Close();
        }

        private GraphChromatogram CreateGraphChrom(string name, string namePosition, bool split)
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
                    graphChrom.Show(paneExisting, null);  // Add to the end
                }
                else
                {
                    var alignment = (graphChrom.Width > graphChrom.Height ?
                        DockPaneAlignment.Right : DockPaneAlignment.Bottom);
                    graphChrom.Show(paneExisting, alignment, 0.5);
                }
            }
            return graphChrom;
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
            // Have to check "DisplayingContents.Count > 0" instead of "IsHidden" here, since "IsHidden" does not get updated when
            // SuspendLayout is on.
            int iPane = dockPanel.Panes.IndexOf(pane => pane.Contents.Contains(dockableForm) && pane.DisplayingContents.Count > 0);
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
                ModifyDocument(string.Format(Resources.SkylineWindow_graphChromatogram_PickedPeak_Pick_peak__0_F01_, e.RetentionTime), 
                    doc => PickPeak(doc, e), docPair =>
                    {
                        var name = GetPropertyName(docPair.OldDoc, e.GroupPath, e.TransitionId);

                        return AuditLogEntry.CreateSimpleEntry(MessageType.picked_peak, docPair.OldDocumentType, name, e.NameSet,
                            e.RetentionTime.MeasuredTime.ToString(@"#.0", CultureInfo.CurrentCulture));
                    });
            }
            finally
            {
                if (graphChrom != null)
                    graphChrom.UnlockZoom();
            }
        }

        private static PropertyName GetPropertyName(SrmDocument doc, IdentityPath groupPath, Identity transitionId)
        {
            var node = doc.FindNode(groupPath);

            if (transitionId != null)
                node = ((TransitionGroupDocNode)node).FindNode(transitionId);

            return AuditLogEntry.GetNodeName(doc, node);
        }

        private void graphChromatogram_ClickedChromatogram(object sender, ClickedChromatogramEventArgs e)
        {
            if (e.ScanProvider != null)
            {
                var dataFile = e.ScanProvider.DataFilePath;
                if (e.ScanIndex == -1)
                {
                    MessageDlg.Show(this, 
                        string.Format(Resources.SkylineWindow_graphChromatogram_ClickedChromatogram_The_raw_file_must_be_re_imported_in_order_to_show_full_scans___0_, dataFile));
                    return;
                }
            }

            ShowGraphFullScan(e.ScanProvider, e.TransitionIndex, e.ScanIndex);
        }

        /// <summary>
        /// Modifies a document in response to the user clicking on a peak in the GraphChromatogram.
        /// </summary>
        private static SrmDocument PickPeak(SrmDocument document, PickedPeakEventArgs e)
        {
            document = document.ChangePeak(e.GroupPath, e.NameSet, e.FilePath, e.TransitionId, e.RetentionTime.MeasuredTime, UserSet.TRUE);
            var activeTransitionGroup = (TransitionGroupDocNode) document.FindNode(e.GroupPath);
            if (activeTransitionGroup.RelativeRT != RelativeRT.Matching)
            {
                return document;
            }
            var activeChromInfo = FindChromInfo(document, activeTransitionGroup, e.NameSet, e.FilePath);
            var peptide = (PeptideDocNode) document.FindNode(e.GroupPath.Parent);
            // See if there are any other transition groups that should have their peak bounds set to the same value
            foreach (var transitionGroup in peptide.TransitionGroups)
            {
                if (transitionGroup.RelativeRT != RelativeRT.Matching)
                {
                    continue;
                }
                var groupPath = new IdentityPath(e.GroupPath.Parent, transitionGroup.TransitionGroup);
                if (Equals(groupPath, e.GroupPath))
                {
                    continue;
                }
                var chromInfo = FindChromInfo(document, transitionGroup, e.NameSet, e.FilePath);
                if (null == chromInfo)
                {
                    continue;
                }
                document = document.ChangePeak(groupPath, e.NameSet, e.FilePath, null, 
                    activeChromInfo.StartRetentionTime, activeChromInfo.EndRetentionTime, UserSet.TRUE, activeChromInfo.Identified, true);
            }
            return document;
        }

        /// <summary>
        /// Finds the TransitionGroupChromInfo that matches the specified ChromatogramSet name and file path.
        /// </summary>
        public static TransitionGroupChromInfo FindChromInfo(SrmDocument document,
            TransitionGroupDocNode transitionGroupDocNode, string nameChromatogramSet, MsDataFileUri filePath)
        {
            ChromatogramSet chromatogramSet;
            int indexSet;
            if (!document.Settings.MeasuredResults.TryGetChromatogramSet(nameChromatogramSet, out chromatogramSet, out indexSet))
            {
                return null;
            }
            var chromFileInfoId = chromatogramSet.FindFile(filePath);
            if (null == chromFileInfoId)
            {
                return null;
            }
            var results = transitionGroupDocNode.Results[indexSet];
            if (results.IsEmpty)
            {
                return null;
            }
            return results.FirstOrDefault(chromInfo => ReferenceEquals(chromFileInfoId, chromInfo.FileId));
        }

        private void graphChromatogram_ChangedPeakBounds(object sender, ChangedMultiPeakBoundsEventArgs eMulti)
        {
            var graphChrom = sender as GraphChromatogram;
            if (graphChrom != null)
                graphChrom.LockZoom();
            try
            {
                string message;
                // Handle most common case of a change to a single group first.
                if (eMulti.Changes.Length == 1)
                {
                    ChangedPeakBoundsEventArgs e = eMulti.Changes[0];
                    if (Equals(e.StartTime, e.EndTime))
                        message = Resources.SkylineWindow_graphChromatogram_ChangedPeakBounds_Remove_peak;
                    else if (e.ChangeType == PeakBoundsChangeType.both)
                        message = string.Format(Resources.SkylineWindow_graphChromatogram_ChangedPeakBounds_Change_peak_to__0_F01___1_F01_, e.StartTime, e.EndTime); 
                    else if (e.ChangeType == PeakBoundsChangeType.start)
                        message = string.Format(Resources.SkylineWindow_graphChromatogram_ChangedPeakBounds_Change_peak_start_to__0_F01_, e.StartTime); 
                    else
                        message = string.Format(Resources.SkylineWindow_graphChromatogram_ChangedPeakBounds_Change_peak_end_to__0_F01_, e.EndTime); 
                }
                else
                {
                    message = Resources.SkylineWindow_graphChromatogram_ChangedPeakBounds_Change_peaks;
                }
                ModifyDocument(message,
                    doc => ChangePeakBounds(Document, eMulti.Changes), docPair =>
                    {
                        var names = eMulti.Changes.Select(change =>
                            GetPropertyName(docPair.OldDoc, change.GroupPath, change.Transition)).ToArray();

                        var messages = eMulti.Changes
                            .SelectMany((change, index) => GetMessagesForPeakBoundsChange(names[index], change))
                            .ToList();
                        if (messages.Count == 1)
                        {
                            return AuditLogEntry.CreateSingleMessageEntry(messages[0]);
                        }                 
                        else if (messages.Count > 1)
                        {
                            var firstName = names.First();
                            if (names.All(name => Equals(name, firstName)))
                            {
                                return AuditLogEntry
                                    .CreateSimpleEntry(MessageType.changed_peak_bounds_of, docPair.OldDocumentType, firstName)
                                    .ChangeAllInfo(messages);
                            }
                            else // TODO: is this even possible?+
                            {
                                return AuditLogEntry
                                    .CreateSimpleEntry(MessageType.changed_peak_bounds, docPair.OldDocumentType)
                                    .ChangeAllInfo(messages);
                            }
                        }

                        return null;

                    });
            }
            finally
            {
                if (graphChrom != null)
                    graphChrom.UnlockZoom();
            }
        }

        private List<MessageInfo> GetMessagesForPeakBoundsChange(PropertyName name, ChangedPeakBoundsEventArgs args)
        {
            var singleTransitionDisplay = args.Transition != null;
            var result = new List<MessageInfo>();

            var transitionGroupDocNode = (TransitionGroupDocNode) Document.FindNode(args.GroupPath);
            var transitionDocNode = singleTransitionDisplay
                ? transitionGroupDocNode.Transitions.FirstOrDefault(tr => ReferenceEquals(tr.Id, args.Transition))
                : null;

            ChromatogramSet chromatograms;
            int indexSet;
            if (!Document.Settings.HasResults ||
                !Document.Settings.MeasuredResults.TryGetChromatogramSet(args.NameSet, out chromatograms, out indexSet))
                return result;

            float? startTime = null;
            float? endTime = null;

            if (singleTransitionDisplay)
            {
                if (transitionDocNode != null)
                {
                    var chromInfo = transitionDocNode.Results[indexSet].FirstOrDefault(ci => ci.OptimizationStep == 0);
                    if (chromInfo != null)
                    {
                        startTime = chromInfo.StartRetentionTime;
                        endTime = chromInfo.EndRetentionTime;
                    }
                }
            }
            else
            {
                var chromInfo = transitionGroupDocNode.Results[indexSet].FirstOrDefault(ci => ci.OptimizationStep == 0);
                if (chromInfo != null)
                {
                    startTime = chromInfo.StartRetentionTime;
                    endTime = chromInfo.EndRetentionTime;
                }
            }

            if (args.ChangeType == PeakBoundsChangeType.start || args.ChangeType == PeakBoundsChangeType.both)
            {
                result.Add(new MessageInfo(
                    singleTransitionDisplay ? MessageType.changed_peak_start : MessageType.changed_peak_start_all,
                    Document.DocumentType,
                    name, args.NameSet, LogMessage.RoundDecimal(startTime, 2),
                    LogMessage.RoundDecimal(args.StartTime.MeasuredTime, 2)));
            }

            if (args.ChangeType == PeakBoundsChangeType.end || args.ChangeType == PeakBoundsChangeType.both)
            {
                result.Add(new MessageInfo(
                    singleTransitionDisplay ? MessageType.changed_peak_end : MessageType.changed_peak_end_all, Document.DocumentType, name,
                    args.NameSet, LogMessage.RoundDecimal(endTime, 2),
                    LogMessage.RoundDecimal(args.EndTime.MeasuredTime, 2)));
            }

            return result;
        }

        /// <summary>
        /// Modifies a document in response to a user's mouse dragging on a GraphChromatogram.
        /// </summary>
        private static SrmDocument ChangePeakBounds(SrmDocument document, IEnumerable<ChangedPeakBoundsEventArgs> changes)
        {
            var changedGroupIds = new HashSet<IdentityPath>();
            var peptideChanges = new Dictionary<IdentityPath, ChangedPeakBoundsEventArgs>();
            foreach (var change in changes)
            {
                document = document.ChangePeak(change.GroupPath, change.NameSet, change.FilePath, change.Transition,
                    change.StartTime.MeasuredTime, change.EndTime.MeasuredTime, UserSet.TRUE, change.Identified, false);
                changedGroupIds.Add(change.GroupPath);
                if (!peptideChanges.ContainsKey(change.GroupPath.Parent)) {
                    var transitionGroup = (TransitionGroupDocNode) document.FindNode(change.GroupPath);
                    if (transitionGroup.RelativeRT == RelativeRT.Matching)
                    {
                        peptideChanges.Add(change.GroupPath.Parent, change);
                    }
                }
            }
            // See if there are any other TransitionGroups that also have RelativeRT matching,
            // and set their peak boundaries to the same.
            foreach (var entry in peptideChanges)
            {
                var peptide = (PeptideDocNode) document.FindNode(entry.Key);
                var change = entry.Value;
                foreach (var transitionGroup in peptide.TransitionGroups)
                {
                    if (transitionGroup.RelativeRT != RelativeRT.Matching)
                    {
                        continue;
                    }
                    var groupId = new IdentityPath(entry.Key, transitionGroup.TransitionGroup);
                    if (changedGroupIds.Contains(groupId))
                    {
                        continue;
                    }
                    if (null == FindChromInfo(document, transitionGroup, change.NameSet, change.FilePath))
                    {
                        continue;
                    }
                    document = document.ChangePeak(groupId, change.NameSet, change.FilePath, null,
                        change.StartTime.MeasuredTime, change.EndTime.MeasuredTime, UserSet.TRUE, change.Identified, true);
                }
            }
            return document;
        }

        private void graphChromatogram_PickedSpectrum(object sender, PickedSpectrumEventArgs e)
        {
            if (_graphSpectrum == null || !_graphSpectrum.Visible)
            {
                ShowGraphSpectrum(true);
            }
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

            // TODO(nicksh): we want to also update GraphSpectrum at this time, but there are issues with
            // this being called reentrantly.
//            if (null != GraphSpectrum)
//            {
//                GraphSpectrum.UpdateUI();
//            }
        }

        private void closeAllChromatogramsMenuItem_Click(object sender, EventArgs e)
        {
            CloseAllChromatograms();
        }

        public void CloseAllChromatograms()
        {
            foreach (var graphChromatogram in _listGraphChrom)
            {
                graphChromatogram.Hide();
            }
        }

        #endregion

        private void splitChromGraphMenuItem_Click(object sender, EventArgs e)
        {
            ShowSplitChromatogramGraph(!Settings.Default.SplitChromatogramGraph);
        }

        public void ShowSplitChromatogramGraph(bool split)
        {
            Settings.Default.SplitChromatogramGraph = split;
            UpdateGraphPanes();
        }

        private void onlyQuantitativeMenuItem_Click(object sender, EventArgs e)
        {
            ShowOnlyQuantitative(!Settings.Default.ShowQuantitativeOnly);
        }

        public void ShowOnlyQuantitative(bool showOnlyQuantitative)
        {
            Settings.Default.ShowQuantitativeOnly = showOnlyQuantitative;
            UpdateGraphPanes();
        }

        /// <summary>
        /// Returns a rectangle suitable for positioning a floating DockableForm.
        /// The size of the rectangle is based off of the size of the DockPanel, and the size of the screen.
        /// </summary>
        private Rectangle GetFloatingRectangleForNewWindow()
        {
            var rectFloat = dockPanel.Bounds;
            rectFloat = dockPanel.RectangleToScreen(rectFloat);
            rectFloat.X += rectFloat.Width / 4;
            rectFloat.Y += rectFloat.Height / 3;
            rectFloat.Width = Math.Max(600, rectFloat.Width / 2);
            rectFloat.Height = Math.Max(440, rectFloat.Height / 2);
            if (Program.SkylineOffscreen)
            {
                var offscreenPoint = GetOffscreenPoint();
                rectFloat.X = offscreenPoint.X;
                rectFloat.Y = offscreenPoint.Y;
            }
            else
            {
                // Make sure it is on the screen.
                var screen = Screen.FromControl(dockPanel);
                var rectScreen = screen.WorkingArea;
                rectFloat.X = Math.Max(rectScreen.X, Math.Min(rectScreen.Width - rectFloat.Width, rectFloat.X));
                rectFloat.Y = Math.Max(rectScreen.Y, Math.Min(rectScreen.Height - rectFloat.Height, rectFloat.Y));
            }
            return rectFloat;
        }

        private bool GraphVisible(IEnumerable<GraphSummary> graphs, GraphTypeSummary type)
        {
            return graphs.Any(g => g.Type == type && !g.IsHidden);
        }

        private bool GraphChecked(IEnumerable<GraphSummary> graphs, IList<GraphTypeSummary> types, GraphTypeSummary type)
        {
            return (types.Contains(type)) && GraphVisible(graphs, type);
        }

        private void ShowGraph(List<GraphSummary> graphs, bool show, GraphTypeSummary type,
            Func<GraphTypeSummary, GraphSummary> createGraph)
        {
            var graph = graphs.FirstOrDefault(g => g.Type == type);
            if (show)
            {
                if (graph != null && !Program.SkylineOffscreen)
                {
                    graphs.Remove(graph);
                    graphs.Insert(0, graph);
                    graph.Controller.GraphTypes.Insert(0, type);

                    if (graphs.Count > 1 && !graphs[1].IsHidden)
                        graph.Show(FindPane(graphs[1]), null);
                    else
                        graph.Activate();
                }
                else
                {
                    if (graph == null)
                        graph = createGraph(type);

                    if (graphs.Count > 1 && !graphs[1].IsHidden)
                    {
                        graph.Show(FindPane(graphs[1]), null);
                    }
                    else
                    {
                        // Choose a position to float the window
                        var rectFloat = GetFloatingRectangleForNewWindow();
                        graph.Show(dockPanel, rectFloat);
                    }
                }
            }
            else if (graph != null)
            {
                graph.Hide();
            }
        }

        #region Retention time graph

        public GraphSummary GraphRetentionTime { get { return _listGraphRetentionTime.FirstOrDefault(); } }

        public bool IsGraphRetentionTimeShown(GraphTypeSummary type)
        {
            return _listGraphRetentionTime.Any(g => g.Type == type && !g.IsHidden);
        }

        private void UpdateUIGraphRetentionTime(Func<GraphTypeSummary, bool> isEnabled)
        {
            var list = Settings.Default.RTGraphTypes.ToArray();
            ShowGraphRetentionTime(isEnabled);
            if (!list.All(isEnabled))
            {
                Settings.Default.RTGraphTypes.Clear();
                Settings.Default.RTGraphTypes.AddRange(list);
            }
        }

        public void ShowGraphRetentionTime(bool show)
        {
            ShowGraphRetentionTime(t => show && IsRetentionTimeGraphTypeEnabled(t));
        }

        private bool IsRetentionTimeGraphTypeEnabled(GraphTypeSummary type)
        {
            bool enabled = DocumentUI.Settings.HasResults;
            switch (type)
            {
                // Can only do run to run regression with at least 2 replicates
                case GraphTypeSummary.run_to_run_regression:
                    return enabled && DocumentUI.Settings.MeasuredResults.Chromatograms.Count > 1;
                // Scheduling can be enabled with a predictor even if there are no results
                case GraphTypeSummary.schedule:
                    return enabled || DocumentUI.Settings.PeptideSettings.Prediction.RetentionTime != null;
                default:
                    return enabled;
            }
        }

        private void ShowGraphRetentionTime(Func<GraphTypeSummary, bool> isEnabled)
        {
            Settings.Default.RTGraphTypes.ToList().ForEach(t =>
                ShowGraphRetentionTime(isEnabled(t), t));
        }

        public void ShowGraphRetentionTime(bool show, GraphTypeSummary type)
        {
            ShowGraph(_listGraphRetentionTime, show, type, CreateGraphRetentionTime);
        }

        private GraphSummary CreateGraphRetentionTime(GraphTypeSummary type)
        {
            if (type == GraphTypeSummary.invalid)
                return null;

            var targetIndex = SelectedResultsIndex;

            var origIndex = -1;
            if(ComboResults != null && ComboResults.Items.Count > 0)
                origIndex = (SelectedResultsIndex + 1) % ComboResults.Items.Count;
            var graph = new GraphSummary(type, this, new RTGraphController(), targetIndex, origIndex);
            graph.FormClosed += graphRetentionTime_FormClosed;
            graph.VisibleChanged += graphRetentionTime_VisibleChanged;
            graph.GraphControl.ZoomEvent += GraphControl_ZoomEvent;
            graph.Toolbar = new RunToRunRegressionToolbar(graph);
            _listGraphRetentionTime.Insert(0, graph);
            

            return graph;
        }

        private void DestroyGraphRetentionTime(GraphSummary graph)
        {
            graph.FormClosed -= graphRetentionTime_FormClosed;
            graph.VisibleChanged -= graphRetentionTime_VisibleChanged;
            graph.HideOnClose = false;
            graph.Close();
            _listGraphRetentionTime.Remove(graph);
            Settings.Default.RTGraphTypes.Remove(graph.Type);
        }

        private void graphRetentionTime_VisibleChanged(object sender, EventArgs e)
        {
            var graph = (GraphSummary) sender;
            if (graph.Visible)
            {
                Settings.Default.RTGraphTypes.Insert(0, graph.Type);
                _listGraphRetentionTime.Remove(graph);
                _listGraphRetentionTime.Insert(0, graph);
            }
            else if (graph.IsHidden)
            {
                Settings.Default.RTGraphTypes.Remove(graph.Type);
            }
        }

        private void graphRetentionTime_FormClosed(object sender, FormClosedEventArgs e)
        {
            GraphSummary graph = (GraphSummary)sender;
            _listGraphRetentionTime.Remove(graph);
            Settings.Default.RTGraphTypes.Remove(graph.Type);
        }

        void GraphSummary.IStateProvider.BuildGraphMenu(ZedGraphControl zedGraphControl, ContextMenuStrip menuStrip, Point mousePt,
            GraphSummary.IController controller)
        {
            ContextMenuGraphSummary = controller.GraphSummary;
            var graphController = controller as RTGraphController;
            if (graphController != null)
                BuildRTGraphMenu(controller.GraphSummary, menuStrip, mousePt, graphController);
            else if (controller is AreaGraphController)
                BuildAreaGraphMenu(controller.GraphSummary, menuStrip, mousePt);
            else if (controller is MassErrorGraphController)
                BuildMassErrorGraphMenu(controller.GraphSummary, menuStrip);
            CopyEmfToolStripMenuItem.AddToContextMenu(zedGraphControl, menuStrip);
        }

        public SrmDocument SelectionDocument
        {
            get { return SequenceTree != null ? SequenceTree.Document : null; }
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
                if (ComboResults != null && 0 <= value && value < ComboResults.Items.Count)
                {
                    var focusStart = User32.GetFocusedControl();
                    ComboResults.SelectedIndex = value;
                    if (focusStart != null)
                    {
                        // Avoid just setting focus back to the chromatogram graph
                        // that just lost activation and reactivating it.
                        if (IsChromatogramGraph(focusStart))
                            dockPanel.ActivePane.Focus();
                        else
                            focusStart.Focus();
                    }
                }
            }
        }

        /// <summary>
        /// Returns true if a control is or belongs to a <see cref="GraphChromatogram"/>.
        /// </summary>
        private static bool IsChromatogramGraph(Control control)
        {
            while (control != null)
            {
                if (control is GraphChromatogram)
                    return true;
                control = control.Parent;
            }
            return false;
        }

        public MsDataFileUri SelectedScanFile { get; set; }
        public double SelectedScanRetentionTime { get; set; }
        public Identity SelectedScanTransition { get; set; }

        public void ActivateReplicate(string name)
        {
            int index;
            ChromatogramSet chromatogramSet;

            if (DocumentUI.Settings.MeasuredResults.TryGetChromatogramSet(name, out chromatogramSet, out index))
            {
                SelectedResultsIndex = index;
            }
        }

        public void SelectPath(IdentityPath focusPath)
        {
            SequenceTree.SelectPath(focusPath);
            UpdateGraphPanes();
        }

        public SpectrumDisplayInfo SelectedSpectrum
        {
            get { return _graphSpectrum != null ? _graphSpectrum.SelectedSpectrum : null; }
        }

        public void ActivateSpectrum()
        {
            ShowGraphSpectrum(true);
        }

        private void BuildRTGraphMenu(GraphSummary graph, ToolStrip menuStrip, Point mousePt, RTGraphController controller)
        {
            // Store original menuitems in an array, and insert a separator
            ToolStripItem[] items = new ToolStripItem[menuStrip.Items.Count];
            int iUnzoom = -1;
            for (int i = 0; i < items.Length; i++)
            {
                items[i] = menuStrip.Items[i];
                string tag = (string)items[i].Tag;
                if (tag == @"unzoom")
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
                    regressionContextMenuItem,
                    schedulingContextMenuItem
                });
            }
            if (regressionContextMenuItem.DropDownItems.Count == 0)
            {
                regressionContextMenuItem.DropDownItems.AddRange(new ToolStripItem[]
                {
                    scoreToRunToolStripMenuItem,
                    runToRunToolStripMenuItem
                });
            }

            GraphTypeSummary graphType = graph.Type;
            if (graphType == GraphTypeSummary.score_to_run_regression || graphType == GraphTypeSummary.run_to_run_regression)
            {
                var runToRun = graphType == GraphTypeSummary.run_to_run_regression;
                menuStrip.Items.Insert(iInsert++, timePlotContextMenuItem);
                if (timePlotContextMenuItem.DropDownItems.Count == 0)
                {
                    timePlotContextMenuItem.DropDownItems.AddRange(new ToolStripItem[]
                    {
                        timeCorrelationContextMenuItem,
                        timeResidualsContextMenuItem
                    });
                }
                timeCorrelationContextMenuItem.Checked = RTGraphController.PlotType == PlotTypeRT.correlation;
                timeResidualsContextMenuItem.Checked = RTGraphController.PlotType == PlotTypeRT.residuals;

                menuStrip.Items.Insert(iInsert++,setRegressionMethodContextMenuItem);
                if (setRegressionMethodContextMenuItem.DropDownItems.Count == 0)
                {
                        setRegressionMethodContextMenuItem.DropDownItems.AddRange(new ToolStripItem[]
                    {
                        linearRegressionContextMenuItem,
                        kernelDensityEstimationContextMenuItem,
                        loessContextMenuItem
                    });
                }
                linearRegressionContextMenuItem.Checked = RTGraphController.RegressionMethod ==
                                                            RegressionMethodRT.linear;
                kernelDensityEstimationContextMenuItem.Checked = RTGraphController.RegressionMethod ==
                                                                   RegressionMethodRT.kde;
                loessContextMenuItem.Checked = RTGraphController.RegressionMethod == RegressionMethodRT.loess;

                var showPointsTypeStandards = Document.GetRetentionTimeStandards().Any();
                var showPointsTypeDecoys = Document.PeptideGroups.Any(nodePepGroup => nodePepGroup.Children.Cast<PeptideDocNode>().Any(nodePep => nodePep.IsDecoy));
                var qvalues = Document.Settings.PeptideSettings.Integration.PeakScoringModel.IsTrained;
                if (showPointsTypeStandards || showPointsTypeDecoys || qvalues)
                {
                    menuStrip.Items.Insert(iInsert++, timePointsContextMenuItem);
                    if (timePointsContextMenuItem.DropDownItems.Count == 0)
                    {
                        timePointsContextMenuItem.DropDownItems.AddRange(new ToolStripItem[]
                        {
                            timeTargetsContextMenuItem,
                            timeStandardsContextMenuItem,
                            timeDecoysContextMenuItem
                        });

                        if (Document.Settings.HasResults &&
                            Document.Settings.PeptideSettings.Integration.PeakScoringModel.IsTrained)
                        {
                            timePointsContextMenuItem.DropDownItems.Insert(1, targetsAt1FDRToolStripMenuItem);
                        }
                    }
                    timeStandardsContextMenuItem.Visible = showPointsTypeStandards;
                    timeDecoysContextMenuItem.Visible = showPointsTypeDecoys;
                    timeTargetsContextMenuItem.Checked = RTGraphController.PointsType == PointsTypeRT.targets;
                    targetsAt1FDRToolStripMenuItem.Checked = RTGraphController.PointsType == PointsTypeRT.targets_fdr;
                    timeStandardsContextMenuItem.Checked = RTGraphController.PointsType == PointsTypeRT.standards;
                    timeDecoysContextMenuItem.Checked = RTGraphController.PointsType == PointsTypeRT.decoys;
                }

                refineRTContextMenuItem.Checked = set.RTRefinePeptides;
                //Grey out so user knows we cannot refine with current regression method
                refineRTContextMenuItem.Enabled = RTGraphController.CanDoRefinementForRegressionMethod;
                menuStrip.Items.Insert(iInsert++, refineRTContextMenuItem);
                if (!runToRun)
                {
                    predictionRTContextMenuItem.Checked = set.RTPredictorVisible;
                    menuStrip.Items.Insert(iInsert++, predictionRTContextMenuItem);
                    iInsert = AddReplicatesContextMenu(menuStrip, iInsert);
                }

                menuStrip.Items.Insert(iInsert++, setRTThresholdContextMenuItem);
                if (!runToRun)
                {
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
                }
                var regressionRT = controller.RegressionRefined;
                createRTRegressionContextMenuItem.Enabled = (regressionRT != null) && !runToRun;
                updateCalculatorContextMenuItem.Visible = (regressionRT != null &&
                    Settings.Default.RTScoreCalculatorList.CanEditItem(regressionRT.Calculator) && !runToRun);
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
            else if (graphType == GraphTypeSummary.schedule)
            {
                menuStrip.Items.Insert(iInsert++, toolStripSeparator38);
                menuStrip.Items.Insert(iInsert++, timePropsContextMenuItem);                
            }
            else
            {
                menuStrip.Items.Insert(iInsert++, toolStripSeparator16);
                menuStrip.Items.Insert(iInsert++, rtValueMenuItem);
                if (rtValueMenuItem.DropDownItems.Count == 0)
                {
                    rtValueMenuItem.DropDownItems.AddRange(new ToolStripItem[]
                    {
                        allRTValueContextMenuItem,
                        timeRTValueContextMenuItem,
                        fwhmRTValueContextMenuItem,
                        fwbRTValueContextMenuItem
                    });
                }
                AddTransitionContextMenu(menuStrip, iInsert++);
                if (graphType == GraphTypeSummary.replicate)
                {
                    iInsert = AddReplicateOrderAndGroupByMenuItems(menuStrip, iInsert);
                    var rtReplicateGraphPane = graph.GraphPanes.FirstOrDefault() as RTReplicateGraphPane;
                    if (rtReplicateGraphPane != null && rtReplicateGraphPane.CanShowRTLegend)
                    {
                        showRTLegendContextMenuItem.Checked = set.ShowRetentionTimesLegend;
                        menuStrip.Items.Insert(iInsert++, showRTLegendContextMenuItem);
                    }
                    if (rtReplicateGraphPane != null)
                    {
                        ChromFileInfoId chromFileInfoId = null;
                        if (DocumentUI.Settings.HasResults)
                        {
                            var chromatogramSet = DocumentUI.Settings.MeasuredResults.Chromatograms[SelectedResultsIndex];
                            if (chromatogramSet.MSDataFileInfos.Count == 1)
                            {
                                chromFileInfoId = chromatogramSet.MSDataFileInfos[0].FileId;
                            }
                        }
                        iInsert = InsertAlignmentMenuItems(menuStrip.Items, chromFileInfoId, iInsert);
                    }
                }
                else if (graphType == GraphTypeSummary.peptide)
                {
                    AddPeptideOrderContextMenu(menuStrip, iInsert++);
                    iInsert = AddReplicatesContextMenu(menuStrip, iInsert);
                    AddScopeContextMenu(menuStrip, iInsert++);
                    InsertAlignmentMenuItems(menuStrip.Items, null, iInsert);
                }
                if (graphType == GraphTypeSummary.peptide || null != SummaryReplicateGraphPane.GroupByReplicateAnnotation)
                {
                    menuStrip.Items.Insert(iInsert++, peptideCvsContextMenuItem);
                    peptideCvsContextMenuItem.Checked = set.ShowPeptideCV;
                }
                selectionContextMenuItem.Checked = set.ShowReplicateSelection;
                menuStrip.Items.Insert(iInsert++, selectionContextMenuItem);
                synchronizeSummaryZoomingContextMenuItem.Checked = set.SynchronizeSummaryZooming;
                menuStrip.Items.Insert(iInsert++, synchronizeSummaryZoomingContextMenuItem);
                menuStrip.Items.Insert(iInsert++, toolStripSeparator38);
                menuStrip.Items.Insert(iInsert++, timePropsContextMenuItem);

                bool canApply, canRemove;
                var isotopeLabelType = graph.GraphPaneFromPoint(mousePt) != null
                    ? graph.GraphPaneFromPoint(mousePt).PaneKey.IsotopeLabelType
                    : null;
                CanApplyOrRemovePeak(removePeakGraphMenuItem.DropDownItems, isotopeLabelType, out canApply, out canRemove);
                if (canApply || canRemove)
                {
                    menuStrip.Items.Insert(iInsert++, toolStripSeparator33);
                    if (canApply)
                    {
                        menuStrip.Items.Insert(iInsert++, applyPeakAllGraphMenuItem);
                        menuStrip.Items.Insert(iInsert++, applyPeakSubsequentGraphMenuItem);
                    }
                    if (canRemove)
                    {
                        menuStrip.Items.Insert(iInsert++, removePeakGraphMenuItem);
                    }
                }
            }

            menuStrip.Items.Insert(iInsert, toolStripSeparator24);

            // Remove some ZedGraph menu items not of interest
            foreach (var item in items)
            {
                string tag = (string)item.Tag;
                if (tag == @"set_default" || tag == @"show_val")
                    menuStrip.Items.Remove(item);
            }
        }

        private void AddScopeContextMenu(ToolStrip menuStrip, int iInsert)
        {
            menuStrip.Items.Insert(iInsert, scopeContextMenuItem);
            if (scopeContextMenuItem.DropDownItems.Count == 0)
            {
                scopeContextMenuItem.DropDownItems.AddRange(new ToolStripItem[]
                {
                    documentScopeContextMenuItem,
                    proteinScopeContextMenuItem
                });
            }
        }

        private int AddReplicatesContextMenu(ToolStrip menuStrip, int iInsert)
        {
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
            return iInsert;
        }

        private void AddPeptideOrderContextMenu(ToolStrip menuStrip, int iInsert)
        {
            menuStrip.Items.Insert(iInsert, peptideOrderContextMenuItem);
            if (peptideOrderContextMenuItem.DropDownItems.Count == 0)
            {
                peptideOrderContextMenuItem.DropDownItems.AddRange(new ToolStripItem[]
                {
                    peptideOrderDocumentContextMenuItem,
                    peptideOrderRTContextMenuItem,
                    peptideOrderAreaContextMenuItem
                });
            }
        }

        private void timeGraphMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            var types = Settings.Default.RTGraphTypes;
            bool runToRunRegression = GraphChecked(_listGraphRetentionTime, types, GraphTypeSummary.run_to_run_regression);
            bool scoreToRunRegression = GraphChecked(_listGraphRetentionTime, types, GraphTypeSummary.score_to_run_regression);

            runToRunToolStripMenuItem.Checked = runToRunRegression;
            scoreToRunToolStripMenuItem.Checked = scoreToRunRegression;
            runToRunMenuItem.Checked = runToRunRegression;
            scoreToRunMenuItem.Checked = scoreToRunRegression;
            regressionMenuItem.Checked = runToRunRegression || scoreToRunRegression;
            regressionContextMenuItem.Checked = runToRunRegression || scoreToRunRegression;

            replicateComparisonMenuItem.Checked = replicateComparisonContextMenuItem.Checked = 
                GraphChecked(_listGraphRetentionTime, types, GraphTypeSummary.replicate);
            timePeptideComparisonMenuItem.Checked = timePeptideComparisonContextMenuItem.Checked = 
                GraphChecked(_listGraphRetentionTime, types, GraphTypeSummary.peptide);
            schedulingMenuItem.Checked = schedulingContextMenuItem.Checked = 
                GraphChecked(_listGraphRetentionTime, types, GraphTypeSummary.schedule);
        }

        private void regressionMenuItem_Click(object sender, EventArgs e)
        {
            ShowRTRegressionGraphScoreToRun();
        }

        private void fullReplicateComparisonToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowRTRegressionGraphRunToRun();
        }

        public void ShowRTRegressionGraphScoreToRun()
        {
            Settings.Default.RTGraphTypes.Insert(0, GraphTypeSummary.score_to_run_regression);
            ShowGraphRetentionTime(true, GraphTypeSummary.score_to_run_regression);
            UpdateRetentionTimeGraph();
        }

        public void ShowRTRegressionGraphRunToRun()
        {
            Settings.Default.RTGraphTypes.Insert(0, GraphTypeSummary.run_to_run_regression);
            ShowGraphRetentionTime(true, GraphTypeSummary.run_to_run_regression);
            UpdateRetentionTimeGraph();
        }

        private void linearRegressionContextMenuItem_Click(object sender, EventArgs e)
        {
            ShowRegressionMethod(RegressionMethodRT.linear);
        }

        private void kernelDensityEstimationContextMenuItem_Click(object sender, EventArgs e)
        {
            ShowRegressionMethod(RegressionMethodRT.kde);
        }

        private void loessContextMenuItem_Click(object sender, EventArgs e)
        {
            ShowRegressionMethod(RegressionMethodRT.loess);
        }

        private void timeCorrelationContextMenuItem_Click(object sender, EventArgs e)
        {
            ShowPlotType(PlotTypeRT.correlation);
        }

        private void timeResidualsContextMenuItem_Click(object sender, EventArgs e)
        {
            ShowPlotType(PlotTypeRT.residuals);
        }

        private void timeTargetsContextMenuItem_Click(object sender, EventArgs e)
        {
            ShowPointsType(PointsTypeRT.targets);
        }

        private void targetsAt1FDRToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (RTLinearRegressionGraphPane.ShowReplicate != ReplicateDisplay.single &&
                RTGraphController.GraphType == GraphTypeSummary.score_to_run_regression)
            {
                using (var dlg = new MultiButtonMsgDlg(
                    Resources.SkylineWindow_targetsAt1FDRToolStripMenuItem_Click_Showing_targets_at_1__FDR_will_set_the_replicate_display_type_to_single__Do_you_want_to_continue_,
                    MultiButtonMsgDlg.BUTTON_YES, MultiButtonMsgDlg.BUTTON_NO, false))
                {
                    if (dlg.ShowDialog(this) != DialogResult.Yes)
                        return;
                }
            }

            ShowSingleReplicate();
            ShowPointsType(PointsTypeRT.targets_fdr);
        }

        private void timeStandardsContextMenuItem_Click(object sender, EventArgs e)
        {
            ShowPointsType(PointsTypeRT.standards);
        }

        private void timeDecoysContextMenuItem_Click(object sender, EventArgs e)
        {
            ShowPointsType(PointsTypeRT.decoys);
        }

        public void ShowPlotType(PlotTypeRT plotTypeRT)
        {
            RTGraphController.PlotType = plotTypeRT;
            UpdateRetentionTimeGraph();
        }

        public void ShowPointsType(PointsTypeRT pointsTypeRT)
        {
            RTGraphController.PointsType = pointsTypeRT;
            UpdateRetentionTimeGraph();
        }

        public void ShowRegressionMethod(RegressionMethodRT regressionMethod)
        {
            RTGraphController.RegressionMethod = regressionMethod;
            UpdateRetentionTimeGraph();
        }

        private void timePeptideComparisonMenuItem_Click(object sender, EventArgs e)
        {
            ShowRTPeptideGraph();
        }

        public void ShowRTPeptideGraph()
        {
            Settings.Default.RTGraphTypes.Insert(0, GraphTypeSummary.peptide);
            ShowGraphRetentionTime(true, GraphTypeSummary.peptide);
            UpdateRetentionTimeGraph();
            SynchronizeSummaryZooming();
        }

        private void showRTLegendContextMenuItem_Click(object sender, EventArgs e)
        {
            ShowRTLegend(!Settings.Default.ShowRetentionTimesLegend);
        }

        public void ShowRTLegend(bool show)
        {
            Settings.Default.ShowRetentionTimesLegend = show;
            UpdateRetentionTimeGraph();
        }

        private void replicateComparisonMenuItem_Click(object sender, EventArgs e)
        {
            ShowRTReplicateGraph();
        }

        public void ShowRTReplicateGraph()
        {
            Settings.Default.RTGraphTypes.Insert(0, GraphTypeSummary.replicate);
            ShowGraphRetentionTime(true, GraphTypeSummary.replicate);
            UpdateRetentionTimeGraph();
            SynchronizeSummaryZooming();
        }

        private void schedulingMenuItem_Click(object sender, EventArgs e)
        {
            ShowRTSchedulingGraph();
        }

        public void ShowRTSchedulingGraph()
        {
            Settings.Default.RTGraphTypes.Insert(0, GraphTypeSummary.schedule);
            ShowGraphRetentionTime(true, GraphTypeSummary.schedule);
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
            ShowRegressionRTThresholdDlg();
        }

        public void ShowRegressionRTThresholdDlg()
        {
            using (var dlg = new RegressionRTThresholdDlg {Threshold = Settings.Default.RTResidualRThreshold})
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

                    ModifyDocument(string.Format(Resources.SkylineWindow_CreateRegression_Set_regression__0__, regression.Name),
                                   doc =>
                                   doc.ChangeSettings(
                                       doc.Settings.ChangePeptidePrediction(p => p.ChangeRetentionTime(regression))), AuditLogEntry.SettingsLogFunction);
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
            var autoItem = new ToolStripMenuItem(Resources.SkylineWindow_SetupCalculatorChooser_Auto, null, delegate { ChooseCalculator(string.Empty); })
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
                        ModifyDocument(string.Format(Resources.SkylineWindow_ShowEditCalculatorDlg_Update__0__calculator, calcNew.Name), doc =>
                            doc.ChangeSettings(doc.Settings.ChangePeptidePrediction(predict =>
                                predict.ChangeRetentionTime(predict.RetentionTime.ChangeCalculator(calcNew)))), AuditLogEntry.SettingsLogFunction);
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
            var outliers = RTGraphController.Outliers;
            var outlierIds = new HashSet<int>();
            foreach (var outlier in outliers)
                outlierIds.Add(outlier.Id.GlobalIndex);

            ModifyDocument(Resources.SkylineWindow_RemoveRTOutliers_Remove_retention_time_outliers,
                doc => (SrmDocument) doc.RemoveAll(outlierIds),
                docPair => AuditLogEntry.CreateCountChangeEntry(MessageType.removed_rt_outlier,
                    MessageType.removed_rt_outliers, docPair.OldDocumentType, RTGraphController.Outliers, outlier =>  MessageArgs.Create(AuditLogEntry.GetNodeName(docPair.OldDoc, outlier)), null));
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

        /// <summary>
        /// If the predicted retention time is auto calculated, add a "Show {Prediction} score" menu item.
        /// If there are retention time alignments available for the specified chromFileInfoId, then adds 
        /// a "Align Times To {Specified File}" menu item to a context menu.
        /// </summary>
        private int InsertAlignmentMenuItems(ToolStripItemCollection items, ChromFileInfoId chromFileInfoId, int iInsert)
        {
            var predictRT = Document.Settings.PeptideSettings.Prediction.RetentionTime;
            if (predictRT != null && predictRT.IsAutoCalculated)
            {
                var menuItem = new ToolStripMenuItem(string.Format(Resources.SkylineWindow_ShowCalculatorScoreFormat, predictRT.Calculator.Name), null, 
                    (sender, eventArgs)=>AlignToRtPrediction=!AlignToRtPrediction)
                    {
                        Checked = AlignToRtPrediction,
                    };
                items.Insert(iInsert++, menuItem);
            }
            if (null != chromFileInfoId && DocumentUI.Settings.HasResults &&
                !DocumentUI.Settings.DocumentRetentionTimes.FileAlignments.IsEmpty)
            {
                foreach (var chromatogramSet in DocumentUI.Settings.MeasuredResults.Chromatograms)
                {
                    var chromFileInfo = chromatogramSet.MSDataFileInfos
                                                       .FirstOrDefault(
                                                           chromFileInfoMatch =>
                                                           ReferenceEquals(chromFileInfoMatch.FileId, chromFileInfoId));
                    if (null == chromFileInfo)
                    {
                        continue;
                    }
                    string fileItemName = Path.GetFileNameWithoutExtension(SampleHelp.GetFileName(chromFileInfo.FilePath));
                    var menuItemText = string.Format(Resources.SkylineWindow_AlignTimesToFileFormat, fileItemName);
                    var alignToFileItem = new ToolStripMenuItem(menuItemText);
                    if (ReferenceEquals(chromFileInfoId, AlignToFile))
                    {
                        alignToFileItem.Click += (sender, eventArgs) => AlignToFile = null;
                        alignToFileItem.Checked = true;
                    }
                    else
                    {
                        alignToFileItem.Click += (sender, eventArgs) => AlignToFile = chromFileInfoId;
                        alignToFileItem.Checked = false;
                    }
                    items.Insert(iInsert++, alignToFileItem);
                }
            }
            return iInsert;
        }

        private void allRTValueContextMenuItem_Click(object sender, EventArgs e)
        {
            // No CVs with all retention time values showing
            Settings.Default.ShowPeptideCV = false;
            ShowRTPeptideValue(RTPeptideValue.All);
        }

        private void timeRTValueContextMenuItem_Click(object sender, EventArgs e)
        {
            ShowRTPeptideValue(RTPeptideValue.Retention);
        }

        private void fwhmRTValueContextMenuItem_Click(object sender, EventArgs e)
        {
            ShowRTPeptideValue(RTPeptideValue.FWHM);
        }

        private void fwbRTValueContextMenuItem_Click(object sender, EventArgs e)
        {
            ShowRTPeptideValue(RTPeptideValue.FWB);
        }

        public void ShowRTPeptideValue(RTPeptideValue value)
        {
            Settings.Default.RTPeptideValue = value.ToString();
            UpdateRetentionTimeGraph();
        }

        private void timePropsContextMenuItem_Click(object sender, EventArgs e)
        {
            ShowRTPropertyDlg(ContextMenuGraphSummary);
        }

        public void ShowRTPropertyDlg(GraphSummary graph)
        {
            if (graph.Type == GraphTypeSummary.schedule)
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

        public void UpdateRetentionTimeGraph()
        {
            _listGraphRetentionTime.ForEach(g =>
            {
                try
                {
                    g.UpdateUI();
                }
                catch (CalculatorException e)
                {
                    MessageDlg.ShowException(this, e);
                    Settings.Default.RTCalculatorName = string.Empty;
                }
            });
        }

        private void retentionTimeAlignmentToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowRetentionTimeAlignmentForm();
        }

        public AlignmentForm ShowRetentionTimeAlignmentForm()
        {
            var form = FormUtil.OpenForms.OfType<AlignmentForm>().FirstOrDefault();
            if (form == null)
            {
                form = new AlignmentForm(this);
                form.Show(this);
            }
            else
            {
                form.Activate();
            }
            return form;
        }

        #endregion

        #region Peak area graph

        public GraphSummary GraphPeakArea { get { return _listGraphPeakArea.FirstOrDefault(); } }

        public void UpdateUIGraphPeakArea(bool visible)
        {
            var list = Settings.Default.AreaGraphTypes.ToArray();
            ShowGraphPeakArea(visible);
            if (!visible)
            {
                Settings.Default.AreaGraphTypes.Clear();
                Settings.Default.AreaGraphTypes.AddRange(list);
            }
        }

        public void ShowGraphPeakArea(bool show)
        {
            Settings.Default.AreaGraphTypes.ToList().ForEach(t => ShowGraphPeakArea(show, t));
        }

        public void ShowGraphPeakArea(bool show, GraphTypeSummary type)
        {
            ShowGraph(_listGraphPeakArea, show, type, CreateGraphPeakArea);
        }

        private GraphSummary CreateGraphPeakArea(GraphTypeSummary type)
        {
            if (type == GraphTypeSummary.invalid)
                return null;

            GraphSummary graph = new GraphSummary(type, this, new AreaGraphController(), SelectedResultsIndex);
            graph.FormClosed += graphPeakArea_FormClosed;
            graph.VisibleChanged += graphPeakArea_VisibleChanged;
            graph.GraphControl.ZoomEvent += GraphControl_ZoomEvent;
            graph.Toolbar = new AreaCVToolbar(graph);
            _listGraphPeakArea.Insert(0, graph);

            return graph;
        }

        private void DestroyGraphPeakArea(GraphSummary graph)
        {
            graph.FormClosed -= graphPeakArea_FormClosed;
            graph.VisibleChanged -= graphPeakArea_VisibleChanged;
            graph.HideOnClose = false;
            graph.Close();
            _listGraphPeakArea.Remove(graph);
            Settings.Default.AreaGraphTypes.Remove(graph.Type);
        }

        private void graphPeakArea_VisibleChanged(object sender, EventArgs e)
        {
            var graph = (GraphSummary)sender;
            if (graph.Visible)
            {
                Settings.Default.AreaGraphTypes.Insert(0, graph.Type);
                _listGraphPeakArea.Remove(graph);
                _listGraphPeakArea.Insert(0, graph);
            }
            else if (graph.IsHidden)
            {
                Settings.Default.AreaGraphTypes.Remove(graph.Type);
            }   
        }

        private void graphPeakArea_FormClosed(object sender, FormClosedEventArgs e)
        {
            GraphSummary graph = (GraphSummary)sender;
            _listGraphPeakArea.Remove(graph);
            Settings.Default.AreaGraphTypes.Remove(graph.Type);
        }

        private void BuildAreaGraphMenu(GraphSummary graphSummary, ToolStrip menuStrip, Point mousePt)
        {
            // Store original menuitems in an array, and insert a separator
            ToolStripItem[] items = new ToolStripItem[menuStrip.Items.Count];
            int iUnzoom = -1;
            for (int i = 0; i < items.Length; i++)
            {
                items[i] = menuStrip.Items[i];
                string tag = (string)items[i].Tag;
                if (tag == @"unzoom")
                    iUnzoom = i;
            }

            if (iUnzoom != -1)
                menuStrip.Items.Insert(iUnzoom, toolStripSeparator25);

            // Insert skyline specific menus
            var set = Settings.Default;
            int iInsert = 0;
            menuStrip.Items.Insert(iInsert++, areaGraphContextMenuItem);
            if (areaGraphContextMenuItem.DropDownItems.Count == 0)
            {
                areaGraphContextMenuItem.DropDownItems.AddRange(new ToolStripItem[]
                {
                    areaReplicateComparisonContextMenuItem,
                    areaPeptideComparisonContextMenuItem,
                    areaCVHistogramContextMenuItem,
                    areaCVHistogram2DContextMenuItem
                });
            }
            var graphType = graphSummary.Type;
            if (graphType == GraphTypeSummary.replicate)
            {
                menuStrip.Items.Insert(iInsert++, graphTypeToolStripMenuItem);
                if (graphTypeToolStripMenuItem.DropDownItems.Count == 0)
                {
                    graphTypeToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[]
                    {
                        barAreaGraphDisplayTypeMenuItem,
                        lineAreaGraphDisplayTypeMenuItem
                    });
                }
 
            }

            menuStrip.Items.Insert(iInsert++, toolStripSeparator16);

            var isHistogram = graphType == GraphTypeSummary.histogram || graphType == GraphTypeSummary.histogram2d;

            if (isHistogram)
                AddGroupByMenuItems(menuStrip, ref iInsert);
            else
                AddTransitionContextMenu(menuStrip, iInsert++);

            if (graphType == GraphTypeSummary.replicate)
            {
                iInsert = AddReplicateOrderAndGroupByMenuItems(menuStrip, iInsert);
                areaNormalizeTotalContextMenuItem.Checked = 
                    (AreaGraphController.AreaView == AreaNormalizeToView.area_percent_view);
                menuStrip.Items.Insert(iInsert++, areaNormalizeContextMenuItem);
                if (areaNormalizeContextMenuItem.DropDownItems.Count == 0)
                {
                    areaNormalizeContextMenuItem.DropDownItems.AddRange(new[]
                        {
                            areaNormalizeGlobalContextMenuItem,
                            areaNormalizeMaximumContextMenuItem,
                            areaNormalizeTotalContextMenuItem,
                            (ToolStripItem)toolStripSeparator40,
                            areaNormalizeNoneContextMenuItem
                        });                 
                }
                var areaReplicateGraphPane = graphSummary.GraphPanes.FirstOrDefault() as AreaReplicateGraphPane;
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
                                                                      ? Resources.SkylineWindow_BuildAreaGraphMenu_Show_Library
                                                                      : Resources.SkylineWindow_BuildAreaGraphMenu_Show_Expected;
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
            else if (graphType == GraphTypeSummary.peptide)
            {
                AddPeptideOrderContextMenu(menuStrip, iInsert++);
                iInsert = AddReplicatesContextMenu(menuStrip, iInsert);
                AddScopeContextMenu(menuStrip, iInsert++);
            }

            if (isHistogram)
            {
                bool trained = Document.Settings.PeptideSettings.Integration.PeakScoringModel.IsTrained;
                bool decoys = Document.Settings.PeptideSettings.Integration.PeakScoringModel.UsesDecoys;
 
                if (trained || decoys)
                {
                    UpdateAreaPointsTypeMenuItems();

                    if (pointsToolStripMenuItem.DropDownItems.Count == 0)
                    {
                        pointsToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[]
                        {
                            areaCVtargetsToolStripMenuItem,
                            areaCVdecoysToolStripMenuItem
                        });
                    }

                    menuStrip.Items.Insert(iInsert++, pointsToolStripMenuItem);
                }

                UpdateAreaCVTransitionsMenuItems();

                if (areaCVTransitionsToolStripMenuItem.DropDownItems.Count == 0)
                {
                    areaCVTransitionsToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[]
                    {
                        areaCVAllTransitionsToolStripMenuItem,
                        areaCVCountTransitionsToolStripMenuItem,
                        areaCVBestTransitionsToolStripMenuItem,
                        toolStripSeparator58,
                        areaCVPrecursorsToolStripMenuItem,
                        areaCVProductsToolStripMenuItem
                    });
                }

                if (areaCVCountTransitionsToolStripMenuItem.DropDownItems.Count == 0)
                {
                    var maxTransCount = Document.PeptideTransitionGroups.Max(g => g.TransitionCount);
                    for (int i = 1; i <= maxTransCount; i++)
                    {
                        var tmp = new ToolStripMenuItem(i.ToString(), null,
                            areaCVCountTransitionsToolStripMenuItem_Click)
                        {
                            Checked = AreaGraphController.AreaCVTransitionsCount == i
                        };
                        areaCVCountTransitionsToolStripMenuItem.DropDownItems.Add(tmp);
                    }
                }

                menuStrip.Items.Insert(iInsert++, areaCVTransitionsToolStripMenuItem);


                UpdateAreaBinWidthMenuItems();
                if (areaCVbinWidthToolStripMenuItem.DropDownItems.Count == 0)
                {
                    areaCVbinWidthToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[]
                    {
                        areaCV05binWidthToolStripMenuItem,
                        areaCV10binWidthToolStripMenuItem,
                        areaCV15binWidthToolStripMenuItem,
                        areaCV20binWidthToolStripMenuItem
                    });
                }
                menuStrip.Items.Insert(iInsert++, areaCVbinWidthToolStripMenuItem);

                areaCVNormalizedToToolStripMenuItem.DropDownItems.Clear();
                UpdateAreaNormalizationMenuItems();
                areaCVNormalizedToToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[]
                {
                    areaCVGlobalStandardsToolStripMenuItem,
                    areaCVMediansToolStripMenuItem,
                    toolStripSeparator54,
                    areaCVNoneToolStripMenuItem
                });
                menuStrip.Items.Insert(iInsert++, areaCVNormalizedToToolStripMenuItem);

                if (graphType == GraphTypeSummary.histogram2d)
                {
                    areaCVLogScaleToolStripMenuItem.Checked = Settings.Default.AreaCVLogScale;
                    menuStrip.Items.Insert(iInsert++, areaCVLogScaleToolStripMenuItem);
                }

                selectionContextMenuItem.Checked = set.ShowReplicateSelection;
                menuStrip.Items.Insert(iInsert++, selectionContextMenuItem);

                menuStrip.Items.Insert(iInsert++, toolStripSeparator57);
                menuStrip.Items.Insert(iInsert++, removeAboveCVCutoffToolStripMenuItem);
            }
            else
            {
                if (graphType == GraphTypeSummary.peptide || !string.IsNullOrEmpty(Settings.Default.GroupByReplicateAnnotation))
                {
                    menuStrip.Items.Insert(iInsert++, peptideCvsContextMenuItem);
                    peptideCvsContextMenuItem.Checked = set.ShowPeptideCV;
                }

                menuStrip.Items.Insert(iInsert++, peptideLogScaleContextMenuItem);
                peptideLogScaleContextMenuItem.Checked = set.AreaLogScale;
                selectionContextMenuItem.Checked = set.ShowReplicateSelection;
                menuStrip.Items.Insert(iInsert++, selectionContextMenuItem);

                synchronizeSummaryZoomingContextMenuItem.Checked = set.SynchronizeSummaryZooming;
                menuStrip.Items.Insert(iInsert++, synchronizeSummaryZoomingContextMenuItem);
            }

            menuStrip.Items.Insert(iInsert++, toolStripSeparator24);
            menuStrip.Items.Insert(iInsert++, areaPropsContextMenuItem);
            menuStrip.Items.Insert(iInsert, toolStripSeparator28);

            if (!isHistogram)
            {
                bool canApply, canRemove;
                var isotopeLabelType = graphSummary.GraphPaneFromPoint(mousePt) != null
                    ? graphSummary.GraphPaneFromPoint(mousePt).PaneKey.IsotopeLabelType
                    : null;
                CanApplyOrRemovePeak(removePeakGraphMenuItem.DropDownItems, isotopeLabelType, out canApply, out canRemove);
                if (canApply || canRemove)
                {
                    menuStrip.Items.Insert(iInsert++, toolStripSeparator33);
                    if (canApply)
                    {
                        menuStrip.Items.Insert(iInsert++, applyPeakAllGraphMenuItem);
                        menuStrip.Items.Insert(iInsert++, applyPeakSubsequentGraphMenuItem);
                    }
                    if (canRemove)
                    {
                        menuStrip.Items.Insert(iInsert++, removePeakGraphMenuItem);
                    }
                }
            }

            // Remove some ZedGraph menu items not of interest
            foreach (var item in items)
            {
                string tag = (string)item.Tag;
                if (tag == @"set_default" || tag == @"show_val")
                    menuStrip.Items.Remove(item);
            }
        }

        private void UpdateAreaCVTransitionsMenuItems()
        {
            areaCVAllTransitionsToolStripMenuItem.Checked = AreaGraphController.AreaCVTransitions == AreaCVTransitions.all;
            areaCVBestTransitionsToolStripMenuItem.Checked = AreaGraphController.AreaCVTransitions == AreaCVTransitions.best;
            var selectedCount = AreaGraphController.AreaCVTransitionsCount;
            for (int i = 0; i < areaCVCountTransitionsToolStripMenuItem.DropDownItems.Count; i++)
            {
                ((ToolStripMenuItem)areaCVCountTransitionsToolStripMenuItem.DropDownItems[i]).Checked =
                    selectedCount - 1 == i;
            }
            areaCVPrecursorsToolStripMenuItem.Checked = AreaGraphController.AreaCVMsLevel == AreaCVMsLevel.precursors;
            areaCVProductsToolStripMenuItem.Checked = AreaGraphController.AreaCVMsLevel == AreaCVMsLevel.products;
        }

        private void areaCVAllTransitionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SetAreaCVTransitions(AreaCVTransitions.all, -1);
        }

        private void areaCVCountTransitionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var item = (ToolStripMenuItem)sender;
            int selectedIdx = ((ToolStripMenuItem)item.OwnerItem).DropDownItems.IndexOf(item) + 1;
            SetAreaCVTransitions(AreaCVTransitions.count, selectedIdx);
        }

        private void areaCVBestTransitionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SetAreaCVTransitions(AreaCVTransitions.best, -1);
        }

        public void SetAreaCVTransitions(AreaCVTransitions transitions, int count)
        {
            AreaGraphController.AreaCVTransitionsCount = count;
            AreaGraphController.AreaCVTransitions = transitions;
            UpdatePeakAreaGraph();
        }

        private void areaCVPrecursorsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SetAreaCVMsLevel(AreaCVMsLevel.precursors);
        }

        private void areaCVProductsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SetAreaCVMsLevel(AreaCVMsLevel.products);
        }

        public void SetAreaCVMsLevel(AreaCVMsLevel msLevel)
        {
            AreaGraphController.AreaCVMsLevel = msLevel;
            UpdatePeakAreaGraph();
        }


        private void UpdateAreaBinWidthMenuItems()
        {
            var factor = AreaGraphController.GetAreaCVFactorToPercentage();
            var unit = Settings.Default.AreaCVShowDecimals ? string.Empty : @"%";

            areaCV05binWidthToolStripMenuItem.Text = 0.5 / factor + unit;
            areaCV10binWidthToolStripMenuItem.Text = 1.0 / factor + unit;
            areaCV15binWidthToolStripMenuItem.Text = 1.5 / factor + unit;
            areaCV20binWidthToolStripMenuItem.Text = 2.0 / factor + unit;

            var binwidth = Settings.Default.AreaCVHistogramBinWidth;
            areaCV05binWidthToolStripMenuItem.Checked = binwidth == 0.5 / factor;
            areaCV10binWidthToolStripMenuItem.Checked = binwidth == 1.0 / factor;
            areaCV15binWidthToolStripMenuItem.Checked = binwidth == 1.5 / factor;
            areaCV20binWidthToolStripMenuItem.Checked = binwidth == 2.0 / factor;
        }

        private void barAreaGraphTypeMenuItem_Click(object sender, EventArgs e)
        {
            SetAreaGraphDisplayType(AreaGraphDisplayType.bars);
        }

        private void lineAreaGraphTypeMenuItem_Click(object sender, EventArgs e)
        {
            SetAreaGraphDisplayType(AreaGraphDisplayType.lines);
        }

        public void SetAreaGraphDisplayType(AreaGraphDisplayType displayType)
        {
            AreaGraphController.GraphDisplayType = displayType;
            barAreaGraphDisplayTypeMenuItem.Checked = (displayType == AreaGraphDisplayType.bars);
            lineAreaGraphDisplayTypeMenuItem.Checked = (displayType == AreaGraphDisplayType.lines);
            UpdatePeakAreaGraph();
        }

        public void SynchronizeSummaryZooming(GraphSummary graphSummary = null, ZoomState zoomState = null)
        {
            var activeGraphSummary = graphSummary ?? dockPanel.ActiveContent as GraphSummary;

            if (!Settings.Default.SynchronizeSummaryZooming || activeGraphSummary == null)
            {
                return;
            }

            var activePane = activeGraphSummary.GraphControl.GraphPane;
                
            GraphSummary[] graphSummaries = new List<GraphSummary>(_listGraphMassError.Concat(_listGraphPeakArea.Concat(_listGraphRetentionTime))).ToArray();
            
            // Find the correct GraphSummary
            int index = graphSummaries.IndexOf(g => ReferenceEquals(g, activeGraphSummary));

            // If zoomstate is null, we use the current state of the active pane
            var xScaleState = zoomState == null ? new ScaleState(activePane.XAxis) : zoomState.XAxis;
            var x2ScaleState = zoomState == null ? new ScaleState(activePane.X2Axis) : zoomState.X2Axis;

            double add = 0.0;

            // If the expected value (library) is visible the zoom has to be shifted
            if (activePane is AreaReplicateGraphPane && (activePane as AreaReplicateGraphPane).IsExpectedVisible)
                add = -1.0;
       
            for (int i = 0; i < graphSummaries.Length; ++i)
            {
                // Make sure we are not syncing the same graph or graphs of different types
                if (i != index && graphSummaries[i] != null && graphSummaries[i].Type == graphSummaries[index].Type && graphSummaries[i].Visible)
                {
                    bool isExpectedVisible = graphSummaries[i].GraphControl.GraphPane is AreaReplicateGraphPane && ((AreaReplicateGraphPane)graphSummaries[i].GraphControl.GraphPane).IsExpectedVisible;
                    
                    if (isExpectedVisible)
                        ++add;

                    graphSummaries[i].GraphControl.GraphPane.XAxis.Scale.Min = xScaleState.Min + add;
                    graphSummaries[i].GraphControl.GraphPane.XAxis.Scale.Max = xScaleState.Max + add;
                    graphSummaries[i].GraphControl.GraphPane.X2Axis.Scale.Min = x2ScaleState.Min + add;
                    graphSummaries[i].GraphControl.GraphPane.X2Axis.Scale.Max = x2ScaleState.Max + add;

                    if (isExpectedVisible)
                        --add;

                    graphSummaries[i].UpdateUI(false);
                }
            }
        }

        void synchronizeSummaryZoomingContextMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.SynchronizeSummaryZooming = synchronizeSummaryZoomingContextMenuItem.Checked;
            SynchronizeSummaryZooming();
        }

        void GraphControl_ZoomEvent(ZedGraphControl sender, ZoomState oldState, ZoomState newState, PointF mousePosition)
        {
            // We pass in a GraphSummary here because sometimes dockPanel.ActiveContent is not the graph the user is zooming in on
            GraphSummary[] graphSummaries = new List<GraphSummary>(_listGraphMassError.Concat(_listGraphPeakArea.Concat(_listGraphRetentionTime))).ToArray();
            SynchronizeSummaryZooming(graphSummaries.FirstOrDefault(gs => gs != null && ReferenceEquals(gs.GraphControl, sender)), newState);
        }

        private void AddGroupByMenuItems(ToolStrip menuStrip, ref int iInsert)
        {
            var groups = AnnotationHelper.FindGroupsByTarget(Document.Settings, AnnotationDef.AnnotationTarget.replicate);
            if (!groups.Any())
            {
                return;
            }

            var item = groupReplicatesByContextMenuItem;
            item.DropDownItems.Clear();

            var all = new ToolStripMenuItem(Resources.SkylineWindow_AddGroupByMenuItems_All_Replicates, null, cvAreaHistogramGroupByMenuItem_Click);
            if (string.IsNullOrEmpty(AreaGraphController.GroupByGroup))
                all.Checked = true;

            item.DropDownItems.Add(all);

            foreach (var g in groups)
            {
                var subItem = new ToolStripMenuItem(g, null, cvAreaHistogramGroupByMenuItem_Click)
                {
                    Checked = AreaGraphController.GroupByGroup == g
                };

                item.DropDownItems.Add(subItem);
            }

            menuStrip.Items.Insert(iInsert++, item);
        }

        private void cvAreaHistogramGroupByMenuItem_Click(object sender, EventArgs e)
        {
            var item = (ToolStripMenuItem) sender;
            string group = null;
            if (((ToolStripMenuItem) item.OwnerItem).DropDownItems.IndexOf(item) != 0)
                group = item.Text;
            SetAreaCVGroup(group);
        }

        private int AddReplicateOrderAndGroupByMenuItems(ToolStrip menuStrip, int iInsert)
        {
            string currentGroupBy = SummaryReplicateGraphPane.GroupByReplicateAnnotation;
            var groupByValues = ReplicateValue.GetGroupableReplicateValues(DocumentUI).ToArray();
            if (groupByValues.Length == 0)
                currentGroupBy = null;

            // If not grouped by an annotation, show the order-by menuitem
            if (string.IsNullOrEmpty(currentGroupBy))
            {
                var orderByReplicateAnnotationDef = groupByValues.FirstOrDefault(
                    value => SummaryReplicateGraphPane.OrderByReplicateAnnotation == value.ToPersistedString());
                menuStrip.Items.Insert(iInsert++, replicateOrderContextMenuItem);
                replicateOrderContextMenuItem.DropDownItems.Clear();
                replicateOrderContextMenuItem.DropDownItems.AddRange(new ToolStripItem[]
                    {
                        replicateOrderDocumentContextMenuItem,
                        replicateOrderAcqTimeContextMenuItem
                    });
                replicateOrderDocumentContextMenuItem.Checked
                    = null == orderByReplicateAnnotationDef &&
                      SummaryReplicateOrder.document == SummaryReplicateGraphPane.ReplicateOrder;
                replicateOrderAcqTimeContextMenuItem.Checked
                    = null == orderByReplicateAnnotationDef &&
                      SummaryReplicateOrder.time == SummaryReplicateGraphPane.ReplicateOrder;
                foreach (var replicateValue in groupByValues)
                {
                    replicateOrderContextMenuItem.DropDownItems.Add(OrderByReplicateAnnotationMenuItem(
                        replicateValue, SummaryReplicateGraphPane.OrderByReplicateAnnotation));
                }
            }
            
            if (groupByValues.Length > 0)
            {
                menuStrip.Items.Insert(iInsert++, groupReplicatesByContextMenuItem);
                groupReplicatesByContextMenuItem.DropDownItems.Clear();
                groupReplicatesByContextMenuItem.DropDownItems.Add(groupByReplicateContextMenuItem);
                groupByReplicateContextMenuItem.Checked = string.IsNullOrEmpty(currentGroupBy);
                foreach (var replicateValue in groupByValues)
                {
                    groupReplicatesByContextMenuItem.DropDownItems
                        .Add(GroupByReplicateAnnotationMenuItem(replicateValue, currentGroupBy));
                }
            }
            return iInsert;
        }

        public ToolStripMenuItem ReplicateOrderContextMenuItem
        {
            get
            {
                return replicateOrderContextMenuItem;
            }
        }

        private ToolStripMenuItem GroupByReplicateAnnotationMenuItem(ReplicateValue replicateValue, string groupBy)
        {
            return new ToolStripMenuItem(replicateValue.Title, null, (sender, eventArgs)=>GroupByReplicateValue(replicateValue))
                       {
                           Checked = replicateValue.ToPersistedString() == groupBy
                       };
        }

        private ToolStripMenuItem OrderByReplicateAnnotationMenuItem(ReplicateValue replicateValue, string currentOrderBy)
        {
            return new ToolStripMenuItem(replicateValue.Title, null,
                                         (sender, eventArgs) => OrderByReplicateAnnotation(replicateValue))
                {
                    Checked = replicateValue.ToPersistedString() == currentOrderBy
                };
        }

        private void removeAboveCVCutoffToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RemoveAboveCVCutoff(ContextMenuGraphSummary);
        }

        public void RemoveAboveCVCutoff(GraphSummary graphSummary)
        {
            var pane = graphSummary.GraphPanes.First() as IAreaCVHistogramInfo;
            if (pane == null ||
                (graphSummary.Type != GraphTypeSummary.histogram && graphSummary.Type != GraphTypeSummary.histogram2d))
                return;

            var cutoff = Settings.Default.AreaCVCVCutoff / AreaGraphController.GetAreaCVFactorToDecimal();
            // Create a set of everything that should remain, so that peptides excluded by
            // the q value cut-off will also be removed
            var ids = new HashSet<int>(pane.CurrentData.Data.Where(d => d.CV < cutoff)
                    .SelectMany(d => d.PeptideAnnotationPairs)
                    .Select(pair => pair.TransitionGroup.Id.GlobalIndex));

            var nodeCount = 0;
            // Remove everything not in the set
            ModifyDocument(Resources.SkylineWindow_RemoveAboveCVCutoff_Remove_peptides_above_CV_cutoff, doc =>
            {
                var setRemove = AreaCVRefinementData.IndicesToRemove(doc, ids);
                nodeCount = setRemove.Count;
                return (SrmDocument)doc.RemoveAll(setRemove, null, (int) SrmDocument.Level.Molecules);
            }, docPair => AuditLogEntry.CreateSimpleEntry(nodeCount == 1 ? MessageType.removed_peptide_above_cutoff : MessageType.removed_peptides_above_cutoff, docPair.OldDocumentType,
                nodeCount, Settings.Default.AreaCVCVCutoff * AreaGraphController.GetAreaCVFactorToPercentage()));
        }

        public void SetAreaCVGroup(string group)
        {
            AreaGraphController.GroupByGroup = group;
            if (string.IsNullOrEmpty(group))
                AreaGraphController.GroupByAnnotation = null;
            UpdatePeakAreaGraph();
        }

        public void SetAreaCVAnnotation(string annotation, bool update = true)
        {
            AreaGraphController.GroupByAnnotation = annotation;

            if(update)
                UpdatePeakAreaGraph();
        }

        private void areaCVtargetsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SetAreaCVPointsType(PointsTypePeakArea.targets);
        }

        private void areaCVdecoysToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SetAreaCVPointsType(PointsTypePeakArea.decoys);
        }

        public void SetAreaCVPointsType(PointsTypePeakArea pointsType)
        {
            AreaGraphController.PointsType = pointsType;
            UpdatePeakAreaGraph();
        }

        private void UpdateAreaPointsTypeMenuItems()
        {
            var pointsType = AreaGraphController.PointsType;
            var shouldUseQValues = AreaGraphController.ShouldUseQValues(Document);
            var decoys = Document.Settings.PeptideSettings.Integration.PeakScoringModel.UsesDecoys;

            if (!decoys && pointsType == PointsTypePeakArea.decoys)
            {
                pointsType = AreaGraphController.PointsType = PointsTypePeakArea.targets;
            }

            areaCVtargetsToolStripMenuItem.Checked = pointsType == PointsTypePeakArea.targets;
            areaCVtargetsToolStripMenuItem.Text = shouldUseQValues ? string.Format(Resources.SkylineWindow_UpdateAreaPointsTypeMenuItems_Targets_at__0___FDR, Settings.Default.AreaCVQValueCutoff * 100.0) : Resources.SkylineWindow_UpdateAreaPointsTypeMenuItems_Targets;
            areaCVdecoysToolStripMenuItem.Visible = decoys;
            areaCVdecoysToolStripMenuItem.Checked = pointsType == PointsTypePeakArea.decoys;
        }

        private void areaGraphMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            var types = Settings.Default.AreaGraphTypes;
            areaReplicateComparisonMenuItem.Checked = areaReplicateComparisonContextMenuItem.Checked = GraphChecked(_listGraphPeakArea, types, GraphTypeSummary.replicate);
            areaPeptideComparisonMenuItem.Checked = areaPeptideComparisonContextMenuItem.Checked = GraphChecked(_listGraphPeakArea, types, GraphTypeSummary.peptide);
            areaCVHistogramMenuItem.Checked = areaCVHistogramContextMenuItem.Checked = GraphChecked(_listGraphPeakArea, types, GraphTypeSummary.histogram);
            areaCVHistogram2DMenuItem.Checked = areaCVHistogram2DContextMenuItem.Checked = GraphChecked(_listGraphPeakArea, types, GraphTypeSummary.histogram2d);
        }

        private void areaCV05binWidthToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var factor = AreaGraphController.GetAreaCVFactorToPercentage();
            SetAreaCVBinWidth(0.5 / factor);
        }

        private void areaCV10binWidthToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var factor = AreaGraphController.GetAreaCVFactorToPercentage();
            SetAreaCVBinWidth(1.0 / factor);
        }

        private void areaCV15binWidthToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var factor = AreaGraphController.GetAreaCVFactorToPercentage();
            SetAreaCVBinWidth(1.5 / factor);
        }

        private void areaCV20binWidthToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var factor = AreaGraphController.GetAreaCVFactorToPercentage();
            SetAreaCVBinWidth(2.0 / factor);
        }

        public void SetAreaCVBinWidth(double binWidth)
        {
            Settings.Default.AreaCVHistogramBinWidth = binWidth;
            UpdatePeakAreaGraph();
        }

        private void areaReplicateComparisonMenuItem_Click(object sender, EventArgs e)
        {
            ShowPeakAreaReplicateComparison();
        }

        public void ShowPeakAreaReplicateComparison()
        {
            Settings.Default.AreaGraphTypes.Insert(0, GraphTypeSummary.replicate);
            ShowGraphPeakArea(true, GraphTypeSummary.replicate);
            UpdatePeakAreaGraph();
            SynchronizeSummaryZooming();
        }

        private void areaPeptideComparisonMenuItem_Click(object sender, EventArgs e)
        {
            ShowPeakAreaPeptideGraph();
        }

        private void areaCVLogScaleToolStripMenuItem_Click(object sender, EventArgs e)
        {
            EnableAreaCVLogScale(!Settings.Default.AreaCVLogScale);
        }

        public void EnableAreaCVLogScale(bool enabled)
        {
            Settings.Default.AreaCVLogScale = enabled;
            UpdatePeakAreaGraph();
        }

        private void UpdateAreaNormalizationMenuItems()
        {
            var mods = DocumentUI.Settings.PeptideSettings.Modifications;
            var standardTypes = mods.RatioInternalStandardTypes;

            if (mods.HasHeavyModifications)
            {
                for (var i = 0; i < standardTypes.Count; i++)
                {
                    var item = new ToolStripMenuItem(standardTypes[i].Title, null, areaCVHeavyModificationToolStripMenuItem_Click)
                    {
                        Checked = AreaGraphController.AreaCVRatioIndex == i && AreaGraphController.NormalizationMethod == AreaCVNormalizationMethod.ratio
                    };
                
                    areaCVNormalizedToToolStripMenuItem.DropDownItems.Insert(i, item);
                }
            }

            areaCVMediansToolStripMenuItem.Checked = AreaGraphController.NormalizationMethod == AreaCVNormalizationMethod.medians;
            areaCVGlobalStandardsToolStripMenuItem.Visible = DocumentUI.Settings.HasGlobalStandardArea;
            areaCVGlobalStandardsToolStripMenuItem.Checked = AreaGraphController.NormalizationMethod == AreaCVNormalizationMethod.global_standards;
            areaCVNoneToolStripMenuItem.Checked = AreaGraphController.NormalizationMethod == AreaCVNormalizationMethod.none;
        }

        private void areaCVHeavyModificationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var item = (ToolStripMenuItem) sender;
            int index = ((ToolStripMenuItem)item.OwnerItem).DropDownItems.IndexOf(item);
            SetNormalizationMethod(AreaCVNormalizationMethod.ratio, index);
        }

        private void areaCVGlobalStandardsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SetNormalizationMethod(AreaCVNormalizationMethod.global_standards);
        }

        private void areaCVMediansToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SetNormalizationMethod(AreaCVNormalizationMethod.medians);
        }

        private void areaCVNoneToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SetNormalizationMethod(AreaCVNormalizationMethod.none);
        }

        public void SetNormalizationMethod(AreaCVNormalizationMethod method, int ratioIndex = -1, bool update = true)
        {
            AreaGraphController.NormalizationMethod = method;
            AreaGraphController.AreaCVRatioIndex = ratioIndex;

            if(update)
                UpdatePeakAreaGraph();
        }

        public void ShowPeakAreaPeptideGraph()
        {
            Settings.Default.AreaGraphTypes.Insert(0, GraphTypeSummary.peptide);
            ShowGraphPeakArea(true, GraphTypeSummary.peptide);
            UpdatePeakAreaGraph();
            SynchronizeSummaryZooming();
        }

        private void areaCVHistogramToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            ShowPeakAreaCVHistogram();
        }

        public void ShowPeakAreaCVHistogram()
        {
            Settings.Default.AreaGraphTypes.Insert(0, GraphTypeSummary.histogram);
            ShowGraphPeakArea(true, GraphTypeSummary.histogram);
            UpdatePeakAreaGraph();
        }

        private void areaCVHistogram2DToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            ShowPeakAreaCVHistogram2D();
        }

        public void ShowPeakAreaCVHistogram2D()
        {
            Settings.Default.AreaGraphTypes.Insert(0, GraphTypeSummary.histogram2d);
            ShowGraphPeakArea(true, GraphTypeSummary.histogram2d);
            UpdatePeakAreaGraph();
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
            SummaryReplicateGraphPane.OrderByReplicateAnnotation = null;
            UpdateSummaryGraphs();
        }

        private void groupByReplicateContextMenuItem_Click(object sender, EventArgs e)
        {
            GroupByReplicateValue(null);
        }

        public void GroupByReplicateValue(ReplicateValue replicateValue)
        {
            SummaryReplicateGraphPane.GroupByReplicateAnnotation = replicateValue?.ToPersistedString();
            UpdateSummaryGraphs();
        }

        public void GroupByReplicateAnnotation(string annotationName)
        {
            SummaryReplicateGraphPane.GroupByReplicateAnnotation =
                DocumentAnnotations.ANNOTATION_PREFIX + annotationName;
            UpdateSummaryGraphs();
        }

        public void OrderByReplicateAnnotation(ReplicateValue replicateValue)
        {
            SummaryReplicateGraphPane.OrderByReplicateAnnotation = replicateValue.ToPersistedString();
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
            peptideOrderMassErrorContextMenuItem.Checked = (peptideOrder == SummaryPeptideOrder.mass_error);
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

        private void peptideOrderMassErrorContextMenuItem_Click(object sender, EventArgs e)
        {
            ShowPeptideOrder(SummaryPeptideOrder.mass_error);
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
            ShowPeptideLogScale(peptideLogScaleContextMenuItem.Checked);
        }

        public void ShowPeptideLogScale(bool isChecked)
        {
            Settings.Default.AreaLogScale = isChecked ;
            if (isChecked)
                AreaGraphController.AreaView = AreaNormalizeToView.none;
            UpdateSummaryGraphs();
        }

        private void peptideCvsContextMenuItem_Click(object sender, EventArgs e)
        {
            ShowCVValues(peptideCvsContextMenuItem.Checked);
        }

        public void ShowCVValues(bool isChecked)
        {
            Settings.Default.ShowPeptideCV = isChecked;
            // Showing CVs only makes sense for Replicates = All
            Settings.Default.ShowRegressionReplicateEnum = ReplicateDisplay.all.ToString();
            // Showing CVs does not make sense for All retention time values at once
            // But this is confusing now, with replicate annotation grouping
//            if (RTPeptideGraphPane.RTValue == RTPeptideValue.All)
//                Settings.Default.RTPeptideValue = RTPeptideValue.Retention.ToString();
            UpdateSummaryGraphs();
        }

        private void areaPropsContextMenuItem_Click(object sender, EventArgs e)
        {
            switch (ContextMenuGraphSummary.Type)
            {
                case GraphTypeSummary.replicate:
                case GraphTypeSummary.peptide:
                    ShowAreaPropertyDlg();
                    break;
                case GraphTypeSummary.histogram:
                case GraphTypeSummary.histogram2d:
                    ShowAreaCVPropertyDlg(ContextMenuGraphSummary);
                    break;
            }       
        }

        public void ShowAreaCVPropertyDlg(GraphSummary graphSummary)
        {

            using (var dlgProperties = new AreaCVToolbarProperties(graphSummary))
            {
                if (dlgProperties.ShowDialog(this) == DialogResult.OK)
                    UpdatePeakAreaGraph();
            }
        }

        public void ShowAreaPropertyDlg()
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
            // Remove menu items up to the "Global Standards" menu item.
            while (!ReferenceEquals(areaNormalizeGlobalContextMenuItem, menu.DropDownItems[0]))
                menu.DropDownItems.RemoveAt(0);
            
            var areaView = AreaGraphController.AreaView;
            var settings = DocumentUI.Settings;
            var mods = settings.PeptideSettings.Modifications;
            var standardTypes = mods.RatioInternalStandardTypes;

            // Add the Heavy option to the areaNormalizeContextMenuItem if there are heavy modifications
            if (mods.HasHeavyModifications)
            {
                for (int i = 0; i < standardTypes.Count; i++)
                {
                    var handler = new SelectNormalizeHandler(this, i);
                    var item = new ToolStripMenuItem(standardTypes[i].Title, null, handler.ToolStripMenuItemClick)
                                   {
                                       Checked = (SequenceTree.RatioIndex == i &&
                                                  areaView == AreaNormalizeToView.area_ratio_view)
                                   };
                    menu.DropDownItems.Insert(i, item);
                }
            }

            bool globalStandard = settings.HasGlobalStandardArea;
            areaNormalizeGlobalContextMenuItem.Visible = globalStandard;
            areaNormalizeGlobalContextMenuItem.Checked = (areaView == AreaNormalizeToView.area_global_standard_view);
            if (!globalStandard && areaView == AreaNormalizeToView.area_global_standard_view)
                areaView = AreaNormalizeToView.none;
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

        public void SetNormalizeIndex(int index)
        {
            new SelectNormalizeHandler(this, index).Select();
        }

        private void areaNormalizeGlobalContextMenuItem_Click(object sender, EventArgs e)
        {
            NormalizeAreaGraphTo(AreaNormalizeToView.area_global_standard_view);
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
            ShowPeakAreaLegend(!Settings.Default.ShowPeakAreaLegend);
        }

        public void ShowPeakAreaLegend(bool show)
        {
            Settings.Default.ShowPeakAreaLegend = show;
            UpdateSummaryGraphs();
        }

        public void UpdatePeakAreaGraph()
        {
            _listGraphPeakArea.ForEach(g => g.UpdateUI());
        }

        private void UpdateSummaryGraphs()
        {
            UpdateRetentionTimeGraph();
            UpdatePeakAreaGraph();    
            UpdateMassErrorGraph();
        }

        #endregion

        #region Mass error graph

        public GraphSummary GraphMassError { get { return _listGraphMassError.FirstOrDefault(); } }

        public void UpdateUIGraphMassError(bool visible)
        {
            var list = Settings.Default.MassErrorGraphTypes.ToArray();
            ShowGraphMassError(visible);
            if (!visible)
            {
                Settings.Default.MassErrorGraphTypes.Clear();
                Settings.Default.MassErrorGraphTypes.AddRange(list);
            }
        }
        public void ShowGraphMassError(bool show)
        {
            Settings.Default.MassErrorGraphTypes.ToList().ForEach(t => ShowGraphMassError(show, t));
        }

        public void ShowGraphMassError(bool show, GraphTypeSummary type)
        {
            ShowGraph(_listGraphMassError, show, type, CreateGraphMassError);
        }

        private GraphSummary CreateGraphMassError(GraphTypeSummary type)
        {
            if (type == GraphTypeSummary.invalid)
                return null;

            var graph = new GraphSummary(type, this, new MassErrorGraphController(), SelectedResultsIndex);
            graph.FormClosed += graphMassError_FormClosed;
            graph.VisibleChanged += graphMassError_VisibleChanged;
            graph.GraphControl.ZoomEvent += GraphControl_ZoomEvent;
            _listGraphMassError.Insert(0, graph);

            return graph;
        }

        private void DestroyGraphMassError(GraphSummary graph)
        {
            graph.FormClosed -= graphMassError_FormClosed;
            graph.VisibleChanged -= graphMassError_VisibleChanged;
            graph.HideOnClose = false;
            graph.Close();
            _listGraphMassError.Remove(graph);
            Settings.Default.MassErrorGraphTypes.Remove(graph.Type);
        }

        private void graphMassError_VisibleChanged(object sender, EventArgs e)
        {
            var graph = (GraphSummary)sender;
            if (graph.Visible)
            {
                Settings.Default.MassErrorGraphTypes.Insert(0, graph.Type);
                _listGraphMassError.Remove(graph);
                _listGraphMassError.Insert(0, graph);
            }
            else if (graph.IsHidden)
            {
                Settings.Default.MassErrorGraphTypes.Remove(graph.Type);
            }   
        }

        private void graphMassError_FormClosed(object sender, FormClosedEventArgs e)
        {
            GraphSummary graph = (GraphSummary) sender;
            _listGraphMassError.Remove(graph);
            Settings.Default.MassErrorGraphTypes.Remove(graph.Type);
        }

        private void massErrorReplicateComparisonMenuItem_Click(object sender, EventArgs e)
        {
            ShowMassErrorReplicateComparison();
        }
        
        public void ShowMassErrorReplicateComparison()
        {
            Settings.Default.MassErrorGraphTypes.Insert(0, GraphTypeSummary.replicate);
            ShowGraphMassError(true, GraphTypeSummary.replicate);
            UpdateMassErrorGraph();
            SynchronizeSummaryZooming();
        }

        private void massErrorPeptideComparisonMenuItem_Click(object sender, EventArgs e)
        {
            ShowMassErrorPeptideGraph();
        }
        
        public void ShowMassErrorPeptideGraph()
        {
            Settings.Default.MassErrorGraphTypes.Insert(0, GraphTypeSummary.peptide);
            ShowGraphMassError(true, GraphTypeSummary.peptide);
            UpdateMassErrorGraph();
            SynchronizeSummaryZooming();
        }

        private void massErrorHistogramMenuItem_Click(object sender, EventArgs e)
        {
            ShowMassErrorHistogramGraph();
        }

        public void ShowMassErrorHistogramGraph()
        {
            Settings.Default.MassErrorGraphTypes.Insert(0, GraphTypeSummary.histogram);
            ShowGraphMassError(true, GraphTypeSummary.histogram);
            UpdateMassErrorGraph();
        }

        private void massErrorHistogram2DMenuItem_Click(object sender, EventArgs e)
        {
            ShowMassErrorHistogramGraph2D();
        }

        public void ShowMassErrorHistogramGraph2D()
        {
            Settings.Default.MassErrorGraphTypes.Insert(0, GraphTypeSummary.histogram2d);
            ShowGraphMassError(true, GraphTypeSummary.histogram2d);
            UpdateMassErrorGraph();
        }

        public void UpdateMassErrorGraph()
        {
           _listGraphMassError.ForEach(g => g.UpdateUI());
        }

        private void massErrorMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            var types = Settings.Default.MassErrorGraphTypes;
            massErrorReplicateComparisonContextMenuItem.Checked = massErrorReplicateComparisonMenuItem.Checked =
                GraphChecked(_listGraphMassError, types, GraphTypeSummary.replicate);
            massErrorPeptideComparisonContextMenuItem.Checked = massErrorPeptideComparisonMenuItem.Checked =
                GraphChecked(_listGraphMassError, types, GraphTypeSummary.peptide);
            massErrorHistogramContextMenuItem.Checked = massErrorHistogramMenuItem.Checked =
                GraphChecked(_listGraphMassError, types, GraphTypeSummary.histogram);
            massErrorHistogram2DContextMenuItem.Checked = massErrorHistogram2DMenuItem.Checked =
                GraphChecked(_listGraphMassError, types, GraphTypeSummary.histogram2d);
        }

        private void BuildMassErrorGraphMenu(GraphSummary graph, ToolStrip menuStrip)
        {
            // Store original menuitems in an array, and insert a separator
            ToolStripItem[] items = new ToolStripItem[menuStrip.Items.Count];
            int iUnzoom = -1;
            for (int i = 0; i < items.Length; i++)
            {
                items[i] = menuStrip.Items[i];
                string tag = (string)items[i].Tag;
                if (tag == @"unzoom")
                    iUnzoom = i;
            }

            if (iUnzoom != -1)
                menuStrip.Items.Insert(iUnzoom, toolStripSeparator25); // TODO: Use another separator?

            // Insert skyline specific menus
            var set = Settings.Default;
            int iInsert = 0;
            var graphType = graph.Type;
            menuStrip.Items.Insert(iInsert++, massErrorGraphContextMenuItem);
            if (massErrorGraphContextMenuItem.DropDownItems.Count == 0) {
                massErrorGraphContextMenuItem.DropDownItems.AddRange(new ToolStripItem[]
                    {
                        massErrorReplicateComparisonContextMenuItem,
                        massErrorPeptideComparisonContextMenuItem,
                        massErrorHistogramContextMenuItem,
                        massErrorHistogram2DContextMenuItem
                    });
            }

            menuStrip.Items.Insert(iInsert++, toolStripSeparator16);
            if (graphType == GraphTypeSummary.peptide ||
                graphType == GraphTypeSummary.replicate)
            {
                AddTransitionContextMenu(menuStrip, iInsert++);
            }
            if (graphType == GraphTypeSummary.replicate)
            {
                iInsert = AddReplicateOrderAndGroupByMenuItems(menuStrip, iInsert);
                var massErrorReplicateGraphPane = graph.GraphPanes.FirstOrDefault() as MassErrorReplicateGraphPane;
                if (massErrorReplicateGraphPane != null)
                {
                    // If the mass error graph is being displayed and it shows a legend, 
                    // display the "Legend" option
                    if (massErrorReplicateGraphPane.CanShowMassErrorLegend)
                    {
                        showMassErrorLegendContextMenuItem.Checked = set.ShowMassErrorLegend; // TODO: Mass error legend
                        menuStrip.Items.Insert(iInsert++, showMassErrorLegendContextMenuItem);
                    }
                }
            }
            else if (graphType == GraphTypeSummary.peptide)
            {
                AddPeptideOrderContextMenu(menuStrip, iInsert++);
                iInsert = AddReplicatesContextMenu(menuStrip, iInsert);
                AddScopeContextMenu(menuStrip, iInsert++);
            }
            else if (graphType == GraphTypeSummary.histogram || graphType == GraphTypeSummary.histogram2d)
            {
                iInsert = AddReplicatesContextMenu(menuStrip, iInsert);
                iInsert = AddPointsContextMenu(menuStrip, iInsert);
                massErrorTargetsContextMenuItem.Checked = MassErrorGraphController.PointsType == PointsTypeMassError.targets;
                massErrorDecoysContextMenuItem.Checked = MassErrorGraphController.PointsType == PointsTypeMassError.decoys;
                bool trained = DocumentUI.Settings.PeptideSettings.Integration.PeakScoringModel.IsTrained;
                massErrorTargets1FDRContextMenuItem.Visible = trained;
                massErrorTargets1FDRContextMenuItem.Checked = MassErrorGraphController.PointsType == PointsTypeMassError.targets_1FDR;
                if (!trained && massErrorTargets1FDRContextMenuItem.Checked)
                {
                    massErrorTargetsContextMenuItem.Checked = true;
                }
                iInsert = AddBinCountContextMenu(menuStrip, iInsert);
                iInsert = AddTransitionsMassErrorContextMenu(menuStrip, iInsert);
            }
            if (graphType == GraphTypeSummary.histogram2d)
            {
                iInsert = AddXAxisContextMenu(menuStrip, iInsert);
                menuStrip.Items.Insert(iInsert++, massErrorlogScaleContextMenuItem);
                massErrorlogScaleContextMenuItem.Checked = Settings.Default.MassErrorHistogram2DLogScale;
            }
            if (graphType == GraphTypeSummary.peptide || (null != Settings.Default.GroupByReplicateAnnotation && graphType == GraphTypeSummary.replicate))
            {
                menuStrip.Items.Insert(iInsert++, peptideCvsContextMenuItem);
                peptideCvsContextMenuItem.Checked = set.ShowPeptideCV;
            }

            if (graphType == GraphTypeSummary.peptide ||
                graphType == GraphTypeSummary.replicate)
            {
                selectionContextMenuItem.Checked = set.ShowReplicateSelection;
                menuStrip.Items.Insert(iInsert++, selectionContextMenuItem);
                synchronizeSummaryZoomingContextMenuItem.Checked = set.SynchronizeSummaryZooming;
                menuStrip.Items.Insert(iInsert++, synchronizeSummaryZoomingContextMenuItem);
            }

            menuStrip.Items.Insert(iInsert++, toolStripSeparator24);
            menuStrip.Items.Insert(iInsert++, massErrorPropsContextMenuItem);
            menuStrip.Items.Insert(iInsert, toolStripSeparator28);

            // Remove some ZedGraph menu items not of interest
            foreach (var item in items)
            {
                string tag = (string)item.Tag;
                if (tag == @"set_default" || tag == @"show_val")
                    menuStrip.Items.Remove(item);
            }
        }

        private int AddPointsContextMenu(ToolStrip menuStrip, int iInsert)
        {
                menuStrip.Items.Insert(iInsert++, massErrorPointsContextMenuItem);
                if (massErrorPointsContextMenuItem.DropDownItems.Count == 0) {
                    massErrorPointsContextMenuItem.DropDownItems.AddRange(new ToolStripItem[]
                    {
                        massErrorTargetsContextMenuItem,
                        massErrorTargets1FDRContextMenuItem,
                        massErrorDecoysContextMenuItem
                    });
                }
            return iInsert;
        }

        private int AddBinCountContextMenu(ToolStrip menuStrip, int iInsert)
        {
            menuStrip.Items.Insert(iInsert++, binCountContextMenuItem);
            if (binCountContextMenuItem.DropDownItems.Count == 0) {
                binCountContextMenuItem.DropDownItems.AddRange(new ToolStripItem[]
                    {
                       ppm05ContextMenuItem,
                       ppm10ContextMenuItem,
                       ppm15ContextMenuItem,
                       ppm20ContextMenuItem
                    });
            }
            return iInsert;
        }

        private int AddTransitionsMassErrorContextMenu(ToolStrip menuStrip, int iInsert)
        {
            menuStrip.Items.Insert(iInsert++, massErrorTransitionsContextMenuItem);
            if (massErrorTransitionsContextMenuItem.DropDownItems.Count == 0) {
                massErrorTransitionsContextMenuItem.DropDownItems.AddRange(new ToolStripItem[]
                    {
                       massErrorAllTransitionsContextMenuItem,
                       massErrorBestTransitionsContextMenuItem,
                       toolStripSeparator55,
                       MassErrorPrecursorsContextMenuItem,
                       MassErrorProductsContextMenuItem
                    });
            }
            return iInsert;
        }
        private int AddXAxisContextMenu(ToolStrip menuStrip, int iInsert)
        {
            menuStrip.Items.Insert(iInsert++, massErrorXAxisContextMenuItem);
            if (massErrorXAxisContextMenuItem.DropDownItems.Count == 0) {
                massErrorXAxisContextMenuItem.DropDownItems.AddRange(new ToolStripItem[]
                    {
                        massErorrRetentionTimeContextMenuItem,
                        massErrorMassToChargContextMenuItem
                    });
            }
            return iInsert;
        }

        private void massErrorTransitionsContextMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            massErrorAllTransitionsContextMenuItem.Checked = MassErrorGraphController.HistogramTransiton == TransitionMassError.all;
            massErrorBestTransitionsContextMenuItem.Checked = MassErrorGraphController.HistogramTransiton == TransitionMassError.best;

            MassErrorPrecursorsContextMenuItem.Checked = MassErrorGraphController.HistogramDisplayType == DisplayTypeMassError.precursors;
            MassErrorProductsContextMenuItem.Checked = MassErrorGraphController.HistogramDisplayType == DisplayTypeMassError.products;
        }

        private void massErrorAllTransitionsContextMenuItem_Click(object sender, EventArgs e)
        {
            ChangeMassErrorTransition(TransitionMassError.all);
        }

        private void massErrorBestTransitionsContextMenuItem_Click(object sender, EventArgs e)
        {
            ChangeMassErrorTransition(TransitionMassError.best);
        }

        public void ChangeMassErrorTransition(TransitionMassError transitionMassError)
        {
            MassErrorGraphController.HistogramTransiton = transitionMassError;
            UpdateMassErrorGraph();
        }

        private void MassErrorPrecursorsContextMenuItem_Click(object sender, EventArgs e)
        {
            ChangeMassErrorDisplayType(DisplayTypeMassError.precursors);
        }

        private void MassErrorProductsContextMenuItem_Click(object sender, EventArgs e)
        {
            ChangeMassErrorDisplayType(DisplayTypeMassError.products);
        }

        public void ChangeMassErrorDisplayType(DisplayTypeMassError displayType)
        {
            MassErrorGraphController.HistogramDisplayType = displayType;
            UpdateMassErrorGraph();
        }

        private void massErrorXAxisContextMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            massErrorMassToChargContextMenuItem.Checked = MassErrorGraphController.Histogram2DXAxis == Histogram2DXAxis.mass_to_charge;
            massErorrRetentionTimeContextMenuItem.Checked = MassErrorGraphController.Histogram2DXAxis == Histogram2DXAxis.retention_time;
        }


        private void massErorrRetentionTimeContextMenuItem_Click(object sender, EventArgs e)
        {
            UpdateXAxis(Histogram2DXAxis.retention_time);
        }

        private void massErrorMassToChargContextMenuItem_Click(object sender, EventArgs e)
        {
            UpdateXAxis(Histogram2DXAxis.mass_to_charge);
        }

        public void UpdateXAxis(Histogram2DXAxis Xaxis)
        {
            MassErrorGraphController.Histogram2DXAxis = Xaxis;
            UpdateMassErrorGraph();
        }

        private void showMassErrorLegendContextMenuItem_Click(object sender, EventArgs e)
        {
            ShowMassErrorLegend(!Settings.Default.ShowMassErrorLegend);
        }

        public void ShowMassErrorLegend(bool show)
        {
            Settings.Default.ShowMassErrorLegend = show;
            UpdateSummaryGraphs();
        }

        private void massErrorlogScaleContextMenuItem_Click(object sender, EventArgs e)
        {
            SwitchLogScale();
        }

        public void SwitchLogScale()
        {
            Settings.Default.MassErrorHistogram2DLogScale = !Settings.Default.MassErrorHistogram2DLogScale;
            UpdateMassErrorGraph();
        }

        private void binCountContextMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            UpdatePpmMenuItem(ppm05ContextMenuItem, 0.5);
            UpdatePpmMenuItem(ppm10ContextMenuItem, 1.0);
            UpdatePpmMenuItem(ppm15ContextMenuItem, 1.5);
            UpdatePpmMenuItem(ppm20ContextMenuItem, 2.0);
        }

        private void UpdatePpmMenuItem(ToolStripMenuItem toolStripMenuItem, double ppm)
        {
            toolStripMenuItem.Checked = Settings.Default.MassErorrHistogramBinSize == ppm;
            toolStripMenuItem.Text = string.Format(@"{0:F01} ppm", ppm);
        }

        private void ppm05ContextMenuItem_Click(object sender, EventArgs e)
        {
            UpdateBinSize(0.5);
        }

        private void ppm10ContextMenuItem_Click(object sender, EventArgs e)
        {
            UpdateBinSize(1);
        }

        private void ppm15ContextMenuItem_Click(object sender, EventArgs e)
        {
            UpdateBinSize(1.5);
        }

        private void ppm20ContextMenuItem_Click(object sender, EventArgs e)
        {
            UpdateBinSize(2);
        }

        public void UpdateBinSize(double bin)
        {
            Settings.Default.MassErorrHistogramBinSize = bin;
            UpdateMassErrorGraph();
        }

        private void massErrorTargetsContextMenuItem_Click(object sender, EventArgs e)
        {
            ShowPointsTypeMassError(PointsTypeMassError.targets);
        }

        private void massErrorDecoysContextMenuItem_Click(object sender, EventArgs e)
        {
            ShowPointsTypeMassError(PointsTypeMassError.decoys);
        }

        private void massErrorTargets1FDRContextMenuItem_Click(object sender, EventArgs e)
        {
            ShowPointsTypeMassError(PointsTypeMassError.targets_1FDR);
        }

        public void ShowPointsTypeMassError(PointsTypeMassError pointsTypeMassError)
        {
            MassErrorGraphController.PointsType = pointsTypeMassError;
            UpdateMassErrorGraph();
        }

        private void massErrorPropsContextMenuItem_Click(object sender, EventArgs e)
        {
            ShowMassErrorPropertyDlg();
        }

        public void ShowMassErrorPropertyDlg()
        {
            using (var dlg = new MassErrorChartPropertyDlg())
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    UpdateSummaryGraphs();
                }
            }
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
                if (_resultsGridForm != null && !Program.SkylineOffscreen)
                {
                    _resultsGridForm.Activate();
                }
                else
                {
                    _resultsGridForm = _resultsGridForm ?? CreateResultsGrid();

                    var rectFloat = GetFloatingRectangleForNewWindow();
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

        private DockableForm CreateResultsGrid()
        {
            Debug.Assert(null == _resultsGridForm);
            _resultsGridForm = new LiveResultsGrid(this);
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
                _resultsGridForm.HideOnClose = false;
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

        #region Document Grid

        private void documentGridMenuItem_Click(object sender, EventArgs e)
        {
            ShowDocumentGrid(true);
        }

        public void ShowDocumentGrid(bool show)
        {
            if (show)
            {
                if (_documentGridForm != null && !Program.SkylineOffscreen)
                {
                    _documentGridForm.Activate();
                }
                else
                {
                    _documentGridForm = _documentGridForm ?? CreateDocumentGrid();
                    if (_documentGridForm != null)
                    {
                        var rectFloat = GetFloatingRectangleForNewWindow();
                        _documentGridForm.Show(dockPanel, rectFloat);
                    }
                }
            }
            else
            {
                if (_documentGridForm != null)
                {
                    _documentGridForm.Close();
                }
            }
            
        }

        private DocumentGridForm CreateDocumentGrid()
        {
            Assume.IsNull(_documentGridForm);
            _documentGridForm = new DocumentGridForm(this);
            _documentGridForm.FormClosed += documentGrid_FormClosed;
            if (!string.IsNullOrEmpty(Settings.Default.DocumentGridView))
            {
                var viewName = ViewName.Parse(Settings.Default.DocumentGridView);
                if (viewName.HasValue)
                {
                    _documentGridForm.DataboundGridControl.ChooseView(viewName.Value);
                }
            }
            return _documentGridForm;
        }

        private void DestroyDocumentGrid()
        {
            if (null != _documentGridForm)
            {
                _documentGridForm.FormClosed -= documentGrid_FormClosed;
                _documentGridForm.Close();
                _documentGridForm = null;
            }
        }

        void documentGrid_FormClosed(object sender, FormClosedEventArgs e)
        {
            _documentGridForm = null;
        }

        #endregion

        #region Calibration Curves

        private void calibrationCurvesMenuItem_Click(object sender, EventArgs e)
        {
            ShowCalibrationForm();
        }

        private CalibrationForm CreateCalibrationForm()
        {
            Assume.IsNull(_calibrationForm);
            _calibrationForm = new CalibrationForm(this);
            _calibrationForm.FormClosed += calibrationForm_FormClosed;
            return _calibrationForm;
        }

        void calibrationForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            _calibrationForm = null;
        }

        private void DestroyCalibrationForm()
        {
            if (null != _calibrationForm)
            {
                _calibrationForm.FormClosed -= calibrationForm_FormClosed;
                _calibrationForm.Close();
                _calibrationForm = null;
            }

        }

        public CalibrationForm ShowCalibrationForm()
        {
            if (null != _calibrationForm)
            {
                _calibrationForm.Activate();
            }
            else
            {
                var rectFloat = GetFloatingRectangleForNewWindow();
                CreateCalibrationForm().Show(dockPanel, rectFloat);
            }
            return _calibrationForm;
        }

        #endregion

        #region Audit Log

        public void ShowAuditLog()
        {
            if (_auditLogForm != null && !Program.SkylineOffscreen)
            {
                _auditLogForm.Activate();
            }
            else
            {
                _auditLogForm = _auditLogForm ?? CreateAuditLogForm();
                if (_auditLogForm != null)
                {
                    var rectFloat = GetFloatingRectangleForNewWindow();
                    _auditLogForm.Show(dockPanel, rectFloat);
                }
            }
        }

        private AuditLogForm CreateAuditLogForm()
        {
            if (_auditLogForm == null)
            {
                _auditLogForm = AuditLogForm.MakeAuditLogForm(this);
                _auditLogForm.FormClosed += _auditLogForm_FormClosed;
            }

            return _auditLogForm;
        }

        private void DestroyAuditLogForm()
        {
            if (_auditLogForm != null)
            {
                _auditLogForm.FormClosed -= _auditLogForm_FormClosed;
                _auditLogForm.Close();
                _auditLogForm = null;
            }
        }

        private void auditLogMenuItem_Click(object sender, EventArgs e)
        {
            ShowAuditLog();
        }

        private void _auditLogForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            _auditLogForm = null;
        }

        public void ClearAuditLog()
        {
            if (!Document.AuditLog.AuditLogEntries.IsRoot)
            {
                ModifyDocument(AuditLogStrings.AuditLogForm__clearLogButton_Click_Clear_audit_log,
                    document => document.ChangeAuditLog(AuditLogEntry.ROOT), docPair => AuditLogEntry.ClearLogEntry(docPair.OldDoc));
            } 
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
            ArrangeGraphs(DisplayGraphsType.Tiled);
        }

        private void arrangeRowMenuItem_Click(object sender, EventArgs e)
        {
            ArrangeGraphs(DisplayGraphsType.Row);
        }

        private void arrangeColumnMenuItem_Click(object sender, EventArgs e)
        {
            ArrangeGraphs(DisplayGraphsType.Column);
        }

        public void ArrangeGraphs(DisplayGraphsType displayGraphsType)
        {
            var listGraphs = GetArrangeableGraphs();
            if (listGraphs.Count < 2)
                return;
            using (new DockPanelLayoutLock(dockPanel, true))
            {
                ArrangeGraphsGrouped(listGraphs, listGraphs.Count, GroupGraphsType.separated, displayGraphsType);
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
                        ArrangeGraphsGrouped(listGraphs, dlg.Groups, dlg.GroupType, dlg.DisplayType);
                    }
                }
            }
        }

        private void ArrangeGraphsGrouped(IList<DockableForm> listGraphs, int groups, GroupGraphsType groupType, DisplayGraphsType displayType)
        {
            // First just arrange everything into a single pane
            ArrangeGraphsTabbed(listGraphs);

            // Figure out how to distribute the panes into rows and columns
            var documentPane = FindPane(listGraphs[0]);
            double width = documentPane.Width;
            double height = documentPane.Height;
            int rows;
            if (displayType == DisplayGraphsType.Row)
            {
                rows = 1;
            }
            else if (displayType == DisplayGraphsType.Column)
            {
                rows = groups;
            }
            else
            {
                rows = 1;
                while ((height / rows) / (width / (groups / rows + (groups % rows > 0 ? 1 : 0))) > MAX_TILED_ASPECT_RATIO)
                    rows++;
            }

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
            List<DockPane> listPanes = dockPanel.Panes
                .Where(pane => !pane.IsHidden && pane.DockState == DockState.Document)
                .ToList();
            if (order == GroupGraphsOrder.Position)
            {
                listPanes.Sort((p1, p2) =>
                {
                    if (p1.Top != p2.Top)
                        return p1.Top - p2.Top;
                    return p1.Left - p2.Left;
                });
                if (reversed)
                    listPanes.Reverse();
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

            if (order != GroupGraphsOrder.Position)
            {
                // Populate a dictionary with the desired document order
                var dictOrder = new Dictionary<DockableForm, int>();
                int iOrder = 0;
                if (_graphSpectrum != null)
                    dictOrder.Add(_graphSpectrum, iOrder++);
                _listGraphRetentionTime.ForEach(g => dictOrder.Add(g, iOrder++));
                _listGraphPeakArea.ForEach(g => dictOrder.Add(g, iOrder++));
                if (DocumentUI.Settings.HasResults)
                {
                    var chromatograms = DocumentUI.Settings.MeasuredResults.Chromatograms.ToList();
                    if (order == GroupGraphsOrder.Acquired_Time)
                    {
                        chromatograms.Sort((c1, c2) =>
                        {
                            var time1 = GetRunStartTime(c1);
                            var time2 = GetRunStartTime(c2);
                            if (!time1.HasValue && !time2.HasValue)
                            {
                                return 0;
                            }
                            else if (!time1.HasValue)
                            {
                                return 1;
                            }
                            else if (!time2.HasValue)
                            {
                                return -1;
                            }
                            return time1.Value.CompareTo(time2.Value);
                        });
                    }
                    foreach (var chromatogramSet in chromatograms)
                    {
                        var graphChrom = GetGraphChrom(chromatogramSet.Name);
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

        public DateTime? GetRunStartTime(ChromatogramSet chromatogramSet)
        {
            DateTime? runStartTime = null;
            foreach (var fileInfo in chromatogramSet.MSDataFileInfos)
            {
                if (!fileInfo.RunStartTime.HasValue)
                {
                    continue;
                }
                if (!runStartTime.HasValue || runStartTime.Value > fileInfo.RunStartTime.Value)
                {
                    runStartTime = fileInfo.RunStartTime;
                }
            }
            return runStartTime;
        }

        #endregion
    }
}
